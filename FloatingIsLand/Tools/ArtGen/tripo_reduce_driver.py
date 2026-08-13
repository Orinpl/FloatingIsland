# -*- coding: utf-8 -*-
"""tripo-v2.0 reduce_face 批量驱动：对已生成的 31 个模型做低模拓扑（减面 + UV 重排 + 贴图重烘）。

- model_file: 用原生成任务 id 现查 get_task_status 拿新鲜 GLB URL（OSS 签名 24h 过期，不能存旧的）
- model_image: 复用 front 视图的上传资产（asset:<vaid>；skill 明确该图只作展示，不进上游 AI）
- 产物: 四边面 FBX（自带 BaseColor/Normal_Bake/Metallic/Roughness 重烘贴图）→ ArtGen/reduced/<id>.fbx
- 429 限流: 与 tripo_driver 同款滚动重排队（并发 ≤3、冷却 120s、每资产最多 6 次）
- 所有 gateway 调用经 bash + shlex.quote 传参：.cmd shim 走 cmd.exe 解析，URL 里的 & 会截断 JSON
"""
import io
import json
import os
import re
import shlex
import shutil
import subprocess
import time

PROJ = r"d:/UnityProject/FloatingIsLand/FloatingIsLand"
ART = os.path.join(PROJ, "ArtGen")
STATE = os.path.join(ART, "state")
OUT = os.path.join(ART, "reduced")
MANIFEST = os.path.join(STATE, "tripo_manifest.tsv")

MAXC = 3
POLL_SEC = 45
RESUBMIT_COOLDOWN = 120
RETRY_MAX = 6
TOTAL_TIMEOUT = 100 * 60
FACE_COUNT = 10000

BASH = shutil.which("bash") or "bash"
CURL = shutil.which("curl") or "curl"
# 注意：gateway 输出是转义 JSON，真实换行以「\ + n」两个可见字符出现，\S 会把
# “\ntask_id:...” 一起吞进 URL（平台报 InvalidURL）。必须把反斜杠/引号排除在 URL 字符集外。
URL_CHARS = r"[^\s\\\"']"
GLB_URL_RE = re.compile(r"https://" + URL_CHARS + r"+?\.glb" + URL_CHARS + r"*")
FBX_URL_RE = re.compile(r"https://" + URL_CHARS + r"+?\.fbx" + URL_CHARS + r"*")


def gw(tool, payload=None, kv=""):
    args = f" --args {shlex.quote(json.dumps(payload, ensure_ascii=True))}" if payload is not None else ""
    cmd = f"atlas-skillhub gateway call-tool --service liclick --tool {tool}{args} {kv}"
    try:
        r = subprocess.run([BASH, "-lc", cmd], capture_output=True, text=True,
                           encoding="utf-8", errors="replace", timeout=180)
        return (r.stdout or "") + (r.stderr or "")
    except subprocess.TimeoutExpired:
        return "GATEWAY-TIMEOUT"


def spath(n):
    return os.path.join(STATE, n)


def read(n):
    p = spath(n)
    return io.open(p, encoding="utf-8").read().strip() if os.path.isfile(p) else None


def write(n, c):
    io.open(spath(n), "w", encoding="utf-8").write(c)


def rm(n):
    p = spath(n)
    if os.path.isfile(p):
        os.remove(p)


def done(aid):
    p = os.path.join(OUT, f"{aid}.fbx")
    return os.path.isfile(p) and os.path.getsize(p) > 100_000


def fresh_model_url(aid):
    tid = read(f"{aid}.3d.task")
    if not tid:
        return None
    out = gw("get_task_status", kv=f"task_id={tid} task_type=model_3d")
    m = GLB_URL_RE.search(out)
    if not m:
        return None
    return m.group(0)


def submit(aid):
    murl = fresh_model_url(aid)
    if not murl:
        write(f"{aid}.rf.fail", "no fresh model url (gen task missing/expired)")
        print(f"[FAIL-url] {aid}", flush=True)
        return False
    vaid = read(f"{aid}.front.vaid")
    if not vaid:
        write(f"{aid}.rf.fail", "no front vaid for model_image")
        print(f"[FAIL-vaid] {aid}", flush=True)
        return False
    payload = {
        "request_type": "reduce_face",
        "model": "tripo-v2.0",
        "model_file": murl,
        "model_image": f"asset:{vaid}",
        "extra_params": {"face_count": FACE_COUNT, "polygon_type": "quadrilateral"},
    }
    out = gw("generate_model_3d", payload=payload)
    m = re.search(r"task_id[\"': ]+([0-9a-f-]{16,})", out)
    if m:
        write(f"{aid}.rf.task", m.group(1))
        print(f"[submit] {aid} -> {m.group(1)}", flush=True)
        return True
    if "429" in out or "exceeded the limit" in out:
        print(f"[submit-429] {aid}", flush=True)
        return "rate-limited"
    write(f"{aid}.rf.fail", out[:2000])
    print(f"[FAIL-submit] {aid}", flush=True)
    return False


def download(aid, url):
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, f"{aid}.fbx")
    r = subprocess.run([CURL, "-sfL", "-o", dst, url], timeout=600)
    if r.returncode == 0 and os.path.getsize(dst) > 100_000:
        print(f"[done] {aid} ({os.path.getsize(dst)} bytes)", flush=True)
        return True
    if os.path.isfile(dst):
        os.remove(dst)
    return False


def poll(aid):
    tid = read(f"{aid}.rf.task")
    if not tid:
        return "failed"
    out = gw("get_task_status", kv=f"task_id={tid} task_type=model_3d")
    low = out.replace("\\\"", "\"")
    if '"status": "Failed"' in low or '"status":"Failed"' in low:
        if "429" in low or "exceeded the limit" in low:
            return "rate-limited"
        write(f"{aid}.rf.fail", low[:2000])
        print(f"[FAIL] {aid}", flush=True)
        return "failed"
    m = FBX_URL_RE.search(out)
    if m:
        return "done" if download(aid, m.group(0)) else "running"
    return "running"


def main():
    ids = []
    for line in io.open(MANIFEST, encoding="utf-8"):
        parts = line.rstrip("\n").split("\t")
        if parts and parts[0]:
            ids.append(parts[0])

    retries = {}
    cooldown_until = 0.0
    start = time.time()

    while time.time() - start < TOTAL_TIMEOUT:
        pending_submit, active = [], []
        finished = failed = 0
        for aid in ids:
            if done(aid):
                finished += 1
            elif read(f"{aid}.rf.fail"):
                failed += 1
            elif read(f"{aid}.rf.task"):
                active.append(aid)
            else:
                pending_submit.append(aid)

        if not active and not pending_submit:
            break

        still = 0
        for aid in active:
            st = poll(aid)
            if st == "rate-limited":
                n = retries.get(aid, 0) + 1
                retries[aid] = n
                rm(f"{aid}.rf.task")
                if n > RETRY_MAX:
                    write(f"{aid}.rf.fail", f"429 x{n}, giving up")
                    print(f"[FAIL-429] {aid}", flush=True)
                else:
                    print(f"[requeue] {aid} 429 第{n}次", flush=True)
                    cooldown_until = max(cooldown_until, time.time() + RESUBMIT_COOLDOWN)
            elif st == "running":
                still += 1
            time.sleep(1)

        if time.time() >= cooldown_until:
            for aid in pending_submit[:max(0, MAXC - still)]:
                r = submit(aid)
                if r == "rate-limited":
                    cooldown_until = time.time() + RESUBMIT_COOLDOWN
                    break
                time.sleep(3)

        print(f"[tick] done={finished} fail={failed} active={still} queue={len(pending_submit)} "
              f"elapsed={int(time.time()-start)}s", flush=True)
        time.sleep(POLL_SEC)

    lines = [f"=== reduce summary {time.strftime('%H:%M:%S')} ==="]
    for aid in ids:
        if done(aid):
            lines.append(f"OK   {aid}  {os.path.getsize(os.path.join(OUT, aid + '.fbx'))}")
        elif read(f"{aid}.rf.fail"):
            lines.append(f"FAIL {aid}")
        else:
            lines.append(f"MISS {aid}")
    txt = "\n".join(lines)
    write("reduce_summary.txt", txt)
    print(txt, flush=True)


if __name__ == "__main__":
    main()
