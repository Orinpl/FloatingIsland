# -*- coding: utf-8 -*-
"""tripo-v3.1 生成驱动（带 429 限流重试 + 并发上限的分批提交）。

背景：一次性提交 31 个任务，平台只放行了 ~10 个并发，其余全部 429
"You have exceeded the limit of generation"。而且网关把任务状态 JSON 转义进
content[0].text，bash 里 grep 原始引号匹配不到 Failed —— 所以换 Python。

行为：
- 读 ArtGen/state/tripo_manifest.tsv（id\thas_side\tside_slot\tprompt，prompt 忽略）
- 已有 ArtGen/gen3d/<id>.* 的跳过；.vaid 上传缓存复用
- 状态机：未提交 → 提交（并发 ≤ MAXC）→ 轮询 → Finished 下载 / 429 重排队（退避）/ 其他失败写 .fail
- 429 重试每个资产最多 RETRY_MAX 次；全部终态后写 tripo_summary.txt
"""
import io
import json
import os
import re
import shutil
import subprocess
import sys
import time

PROJ = r"d:/UnityProject/FloatingIsLand/FloatingIsLand"
ART = os.path.join(PROJ, "ArtGen")
STATE = os.path.join(ART, "state")
GEN = os.path.join(ART, "gen3d")
MANIFEST = os.path.join(STATE, "tripo_manifest.tsv")

MAXC = 3            # 同时在跑的任务上限（平台限流保守值）
POLL_SEC = 45       # 轮询间隔
RESUBMIT_COOLDOWN = 120  # 429 后重提前的冷却
RETRY_MAX = 6
TOTAL_TIMEOUT = 100 * 60
# 目标面数：不传时平台默认 500000（≈18MB/FBX，31 个 580MB）。平台 reduce_face 后减
# 会把复杂建筑减崩（细柱/薄墙塌成片），所以在生成端原生限面。范围 500~2000000。
# 20000 对齐旧版 rodin 资产的量级（实测 polyIdx≈57k/3 ≈ 1.9 万三角、单文件 ~1.4MB）。
FACE_COUNT = 20000

MODEL_URL_RE = re.compile(r"https://[^\"'\\\s]+\.(?:fbx|glb|obj|stl|usdz|zip)[^\"'\\\s]*")


ATLAS = shutil.which("atlas-skillhub") or "atlas-skillhub"
CURL = shutil.which("curl") or "curl"


def gw(tool, args_json=None, kv=None):
    cmd = [ATLAS, "gateway", "call-tool", "--service", "liclick", "--tool", tool]
    if args_json is not None:
        cmd += ["--args", json.dumps(args_json, ensure_ascii=False)]
    if kv:
        cmd += kv
    try:
        out = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8",
                             errors="replace", timeout=180)
        return (out.stdout or "") + (out.stderr or "")
    except subprocess.TimeoutExpired:
        return "GATEWAY-TIMEOUT"


def spath(name):
    return os.path.join(STATE, name)


def read(name):
    p = spath(name)
    if os.path.isfile(p):
        return io.open(p, encoding="utf-8").read().strip()
    return None


def write(name, content):
    io.open(spath(name), "w", encoding="utf-8").write(content)


def rm(name):
    p = spath(name)
    if os.path.isfile(p):
        os.remove(p)


def done(aid):
    if not os.path.isdir(GEN):
        return False
    return any(f.startswith(aid + ".") for f in os.listdir(GEN))


def upload_view(aid, view):
    cache = f"{aid}.{view}.vaid"
    aidc = read(cache)
    if aidc:
        return aidc
    f = os.path.join(ART, "views", f"{aid}.{view}.jpg")
    if not os.path.isfile(f):
        return None
    out = gw("upload_asset", kv=["--file", f"file_path={f}", "asset_type=image"])
    m = re.search(r"asset_id[\"': ]+([A-Za-z0-9_-]+)", out)
    if m:
        write(cache, m.group(1))
        return m.group(1)
    return None


def submit(item):
    aid, has_side, side_slot = item["id"], item["has_side"], item["side_slot"]
    fa = upload_view(aid, "front")
    if not fa:
        write(f"{aid}.3d.fail", "upload front failed")
        return False
    args = {"request_type": "generation", "model": "tripo-v3.1", "front": f"asset:{fa}",
            "extra_params": {"face_count": FACE_COUNT}}
    if has_side:
        sa = upload_view(aid, "side")
        if sa:
            args[side_slot] = f"asset:{sa}"
    out = gw("generate_model_3d", args_json=args)
    m = re.search(r"task_id[\"': ]+([0-9a-f-]{16,})", out)
    if m:
        write(f"{aid}.3d.task", m.group(1))
        print(f"[submit] {aid} -> {m.group(1)}", flush=True)
        return True
    mu = MODEL_URL_RE.search(out)
    if mu:
        write(f"{aid}.3d.url", mu.group(0))
        print(f"[fast-done] {aid}", flush=True)
        return True
    write(f"{aid}.3d.fail", out[:2000])
    print(f"[FAIL-submit] {aid}", flush=True)
    return False


def download(aid, url):
    os.makedirs(GEN, exist_ok=True)
    ext = url.split("?")[0].rsplit(".", 1)[-1].lower()
    if ext not in ("fbx", "glb", "obj", "stl", "usdz", "zip"):
        ext = "glb"
    dst = os.path.join(GEN, f"{aid}.{ext}")
    r = subprocess.run([CURL, "-sfL", "-o", dst, url], timeout=600)
    if r.returncode == 0 and os.path.getsize(dst) > 10000:
        print(f"[done] {aid} ({ext})", flush=True)
        return True
    if os.path.isfile(dst):
        os.remove(dst)
    rm(f"{aid}.3d.url")
    return False


def poll(aid):
    """返回 'running' | 'done' | 'failed' | 'rate-limited'。"""
    url = read(f"{aid}.3d.url")
    if url:
        return "done" if download(aid, url) else "running"
    tid = read(f"{aid}.3d.task")
    if not tid:
        return "failed"
    out = gw("get_task_status", kv=[f"task_id={tid}", "task_type=model_3d"])
    low = out.replace("\\\"", "\"")
    if '"status": "Failed"' in low or '"status":"Failed"' in low:
        if "429" in low or "exceeded the limit" in low:
            return "rate-limited"
        write(f"{aid}.3d.fail", low[:2000])
        print(f"[FAIL] {aid}", flush=True)
        return "failed"
    m = MODEL_URL_RE.search(out)
    if m:
        write(f"{aid}.3d.url", m.group(0))
        thumb = re.search(r"https://[^\"'\\\s]+\.(?:jpg|jpeg|png|webp)[^\"'\\\s]*", out)
        if thumb and not read(f"{aid}.3d.thumb"):
            write(f"{aid}.3d.thumb", thumb.group(0))
        return "done" if download(aid, m.group(0)) else "running"
    return "running"


def main():
    items = []
    for line in io.open(MANIFEST, encoding="utf-8"):
        parts = line.rstrip("\n").split("\t")
        if len(parts) < 3 or not parts[0]:
            continue
        items.append({"id": parts[0], "has_side": parts[1] == "1", "side_slot": parts[2]})

    retries = {}
    cooldown_until = 0.0
    start = time.time()

    while time.time() - start < TOTAL_TIMEOUT:
        pending_submit = []
        active = []
        finished = 0
        failed = 0
        for it in items:
            aid = it["id"]
            if done(aid):
                finished += 1
            elif read(f"{aid}.3d.fail"):
                failed += 1
            elif read(f"{aid}.3d.task") or read(f"{aid}.3d.url"):
                active.append(it)
            else:
                pending_submit.append(it)

        if not active and not pending_submit:
            break

        # 轮询在跑的
        still_active = 0
        for it in active:
            aid = it["id"]
            st = poll(aid)
            if st == "rate-limited":
                n = retries.get(aid, 0) + 1
                retries[aid] = n
                rm(f"{aid}.3d.task")
                if n > RETRY_MAX:
                    write(f"{aid}.3d.fail", f"429 rate limited x{n}, giving up")
                    print(f"[FAIL-429] {aid} 重试超限", flush=True)
                else:
                    print(f"[requeue] {aid} 429 第{n}次，冷却 {RESUBMIT_COOLDOWN}s", flush=True)
                    cooldown_until = max(cooldown_until, time.time() + RESUBMIT_COOLDOWN)
            elif st == "running":
                still_active += 1
            time.sleep(1)

        # 补位提交（限并发 + 冷却窗口）
        if time.time() >= cooldown_until:
            slots = MAXC - still_active
            for it in pending_submit[:max(0, slots)]:
                submit(it)
                time.sleep(3)

        print(f"[tick] done={finished} fail={failed} active={still_active} "
              f"queue={len(pending_submit)} elapsed={int(time.time()-start)}s", flush=True)
        time.sleep(POLL_SEC)

    # 汇总
    lines = [f"=== tripo driver summary {time.strftime('%H:%M:%S')} ==="]
    for it in items:
        aid = it["id"]
        if done(aid):
            f = next(f for f in os.listdir(GEN) if f.startswith(aid + "."))
            lines.append(f"OK   {aid}  {f}")
        elif read(f"{aid}.3d.fail"):
            lines.append(f"FAIL {aid}")
        else:
            lines.append(f"MISS {aid}")
    txt = "\n".join(lines)
    write("tripo_summary.txt", txt)
    print(txt, flush=True)


if __name__ == "__main__":
    main()
