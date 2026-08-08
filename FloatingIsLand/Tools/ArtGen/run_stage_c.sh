#!/bin/bash
# 阶段C：三视图 → rodin-gen-2.5 生成 low poly 3D 模型（FBX），下载到 Assets/Res/<id>/fbx/<id>.fbx
# 视图优先用本地文件上传（asset:<id>），避免 URL 过期
set -u
PROJ="d:/UnityProject/FloatingIsLand/FloatingIsLand"
ART="$PROJ/ArtGen"          # 中间产物目录（大文件，仓库忽略）
SCRIPTDIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STATE="$ART/state"
MANIFEST="$SCRIPTDIR/manifest.tsv"
mkdir -p "$STATE"

statuscall() { atlas-skillhub gateway call-tool --service liclick --tool get_task_status task_id="$1" task_type=model_3d 2>&1; }

uploadview() { # $1=id $2=view → echo asset_id（优先用压缩版，避免网关 413）
  local f="$ART/views/$1.$2.jpg" cache="$STATE/$1.$2.vaid"
  [ -f "$f" ] || f="$PROJ/Assets/Res/$1/picture/$2.png"
  [ -f "$cache" ] && { cat "$cache"; return 0; }
  local out aid
  out=$(atlas-skillhub gateway call-tool --service liclick --tool upload_asset --file file_path="$f" asset_type=image 2>&1)
  aid=$(echo "$out" | grep -oE 'asset_id["'"'"': ]+[A-Za-z0-9_-]+' | head -1 | grep -oE '[A-Za-z0-9_-]+$')
  [ -n "$aid" ] && echo "$aid" > "$cache" && echo "$aid"
}

# ---- 提交 ----
while IFS=$'\t' read -r id aspect prompt; do
  [ -z "$id" ] && continue
  [ -f "$PROJ/Assets/Res/$id/fbx/$id.fbx" ] && { echo "[skip] $id 已有模型"; continue; }
  [ -f "$STATE/$id.3d.task" ] && { echo "[skip] $id 已提交"; continue; }
  ok=1
  for v in front side top; do [ -f "$PROJ/Assets/Res/$id/picture/$v.png" ] || ok=0; done
  [ "$ok" -eq 0 ] && { echo "[skip] $id 三视图不齐"; continue; }

  fa=$(uploadview "$id" front); sa=$(uploadview "$id" side); ta=$(uploadview "$id" top)
  if [ -z "$fa" ] || [ -z "$sa" ] || [ -z "$ta" ]; then echo "[FAIL-upload-views] $id"; echo "upload views failed" > "$STATE/$id.3d.fail"; continue; fi

  args=$(printf '{"request_type":"generation","model":"rodin-gen-2.5","front":"asset:%s","left":"asset:%s","top":"asset:%s","prompt":"low poly stylized game building asset, faceted geometry, flat shading, clean silhouette, matches the reference views exactly","extra_params":{"geometry_file_format":"fbx","material":"PBR","mesh_mode":"Raw","face_count":20000}}' "$fa" "$sa" "$ta")
  out=$(atlas-skillhub gateway call-tool --service liclick --tool generate_model_3d --args "$args" 2>&1)
  tid=$(echo "$out" | grep -oE 'task_id: [0-9a-f-]+' | head -1 | cut -d' ' -f2)
  if [ -n "$tid" ]; then echo "$tid" > "$STATE/$id.3d.task"; echo "[submit] $id -> $tid"
  else
    url=$(echo "$out" | grep -oE 'https://[^"\\ ]+' | head -1)
    if [ -n "$url" ]; then echo "$url" > "$STATE/$id.3d.url"; echo "[fast-done] $id"; else echo "$out" > "$STATE/$id.3d.fail"; echo "[FAIL-submit] $id"; fi
  fi
  sleep 3
done < "$MANIFEST"

# ---- 轮询 + 下载（3D 任务 3~10 分钟，轮询 60s x 40 轮）----
for round in $(seq 1 40); do
  pending=0
  while IFS=$'\t' read -r id aspect prompt; do
    [ -z "$id" ] && continue
    dest="$PROJ/Assets/Res/$id/fbx/$id.fbx"
    [ -f "$dest" ] && continue
    [ -f "$STATE/$id.3d.fail" ] && continue
    [ -f "$STATE/$id.3d.task" ] || [ -f "$STATE/$id.3d.url" ] || continue
    url=""
    [ -f "$STATE/$id.3d.url" ] && url=$(cat "$STATE/$id.3d.url")
    if [ -z "$url" ]; then
      tid=$(cat "$STATE/$id.3d.task")
      out=$(statuscall "$tid")
      if echo "$out" | grep -q '"status": *"Failed"'; then echo "$out" > "$STATE/$id.3d.fail"; echo "[FAIL] $id"; continue; fi
      # 只认模型文件扩展名的 URL —— Processing 期间响应里就带 thumbnail(jpg)，
      # 取「第一个 https」会把缩略图当模型下载下来
      url=$(echo "$out" | grep -oE 'https://[^"\\ ]+\.(fbx|glb|obj|stl|usdz|zip)[^"\\ ]*' | head -1)
      [ -n "$url" ] && echo "$url" > "$STATE/$id.3d.url"
    fi
    if [ -n "$url" ]; then
      mkdir -p "$PROJ/Assets/Res/$id/fbx" "$PROJ/Assets/Res/$id/mat"
      ext="fbx"; echo "$url" | grep -q '\.glb' && ext="glb"; echo "$url" | grep -q '\.obj' && ext="obj"
      if curl -sfL -o "$PROJ/Assets/Res/$id/fbx/$id.$ext" "$url"; then echo "[done] $id ($ext)"; else rm -f "$STATE/$id.3d.url"; pending=$((pending+1)); fi
    else
      pending=$((pending+1))
    fi
  done < "$MANIFEST"
  [ "$pending" -eq 0 ] && break
  echo "[round $round] pending=$pending"
  sleep 60
done

# ---- 汇总 ----
{
  echo "=== Stage C summary $(date +%H:%M:%S) ==="
  while IFS=$'\t' read -r id aspect prompt; do
    [ -z "$id" ] && continue
    if ls "$PROJ/Assets/Res/$id/fbx/$id".* >/dev/null 2>&1; then echo "OK   $id"
    elif [ -f "$STATE/$id.3d.fail" ]; then echo "FAIL $id"
    else echo "MISS $id"; fi
  done < "$MANIFEST"
} > "$STATE/stageC_summary.txt"
cat "$STATE/stageC_summary.txt"
