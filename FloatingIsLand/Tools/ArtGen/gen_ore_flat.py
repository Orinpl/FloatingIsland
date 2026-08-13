# -*- coding: utf-8 -*-
"""一次性：用矿石概念图右侧的矮圆造型（ore_flat.front.jpg）重生成扁平 ore。
上传 → tripo-v3.1 generation（face_count=20000，单 front 视图）→ 轮询 → 下载覆盖 gen3d/ore.glb。
旧的高尖版先挪到 ArtGen/try_ore/ore_tall.glb。gateway 约定与 tripo_driver 相同。
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
VIEW = ART.replace("\\", "/") + "/views/ore_flat.front.jpg"  # bash -lc 下反斜杠会被吃掉
GEN = os.path.join(ART, "gen3d")

BASH = shutil.which("bash") or "bash"
CURL = shutil.which("curl") or "curl"
URL_CHARS = r"[^\s\\\"']"
GLB_URL_RE = re.compile(r"https://" + URL_CHARS + r"+?\.glb" + URL_CHARS + r"*")


def gw(tool, payload=None, kv=""):
    args = f" --args {shlex.quote(json.dumps(payload, ensure_ascii=True))}" if payload is not None else ""
    cmd = f"atlas-skillhub gateway call-tool --service liclick --tool {tool}{args} {kv}"
    r = subprocess.run([BASH, "-lc", cmd], capture_output=True, text=True,
                       encoding="utf-8", errors="replace", timeout=180)
    return (r.stdout or "") + (r.stderr or "")


def main():
    cache = os.path.join(STATE, "ore_flat.front.vaid")
    vaid = io.open(cache, encoding="utf-8").read().strip() if os.path.isfile(cache) else None
    if not vaid:
        out = gw("upload_asset", kv=f"--file file_path={shlex.quote(VIEW)} asset_type=image")
        m = re.search(r"asset_id[\"': ]+([A-Za-z0-9_-]+)", out)
        if not m:
            print("UPLOAD-FAIL\n" + out[:800], flush=True)
            return
        vaid = m.group(1)
        io.open(cache, "w", encoding="utf-8").write(vaid)
    print(f"[vaid] {vaid}", flush=True)

    payload = {"request_type": "generation", "model": "tripo-v3.1",
               "front": f"asset:{vaid}", "extra_params": {"face_count": 20000}}
    out = gw("generate_model_3d", payload=payload)
    m = re.search(r"task_id[\"': ]+([0-9a-f-]{16,})", out)
    if not m:
        print("SUBMIT-FAIL\n" + out[:800], flush=True)
        return
    tid = m.group(1)
    io.open(os.path.join(STATE, "ore_flat.3d.task"), "w", encoding="utf-8").write(tid)
    print(f"[submit] {tid}", flush=True)

    deadline = time.time() + 20 * 60
    while time.time() < deadline:
        time.sleep(30)
        out = gw("get_task_status", kv=f"task_id={tid} task_type=model_3d")
        low = out.replace("\\\"", "\"")
        if '"status": "Failed"' in low or '"status":"Failed"' in low:
            print("GEN-FAIL\n" + low[:800], flush=True)
            return
        m = GLB_URL_RE.search(out)
        if m:
            old = os.path.join(GEN, "ore.glb")
            os.makedirs(os.path.join(ART, "try_ore"), exist_ok=True)
            if os.path.isfile(old):
                shutil.move(old, os.path.join(ART, "try_ore", "ore_tall.glb"))
            r = subprocess.run([CURL, "-sfL", "-o", old, m.group(0)], timeout=600)
            if r.returncode == 0 and os.path.getsize(old) > 100_000:
                print(f"[done] ore.glb {os.path.getsize(old)} bytes", flush=True)
            else:
                print("DOWNLOAD-FAIL", flush=True)
            return
        print("[poll] running", flush=True)
    print("TIMEOUT", flush=True)


if __name__ == "__main__":
    main()
