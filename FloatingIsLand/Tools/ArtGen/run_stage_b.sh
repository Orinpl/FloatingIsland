#!/bin/bash
# 阶段B：对每个已有 concept.png 的资产，上传参考图 → 生成三视图（正面/侧面/俯视）
# 产物：Assets/Res/<id>/picture/{front,side,top}.png；视图 URL 存 state/<id>.<view>.url（给阶段C用）
set -u
PROJ="d:/UnityProject/FloatingIsLand/FloatingIsLand"
ART="$PROJ/ArtGen"          # 中间产物目录（大文件，仓库忽略）
SCRIPTDIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STATE="$ART/state"
MANIFEST="$SCRIPTDIR/manifest.tsv"
COMMON="keep identical colors materials and proportions as the reference image, plain very light blue-grey background, low poly 3D game art, faceted geometry, flat shading, no text, no watermark"
mkdir -p "$STATE"

statuscall() { atlas-skillhub gateway call-tool --service liclick --tool get_task_status task_id="$1" task_type=image 2>&1; }

viewprompt() { # $1=view
  case "$1" in
    front) echo "Orthographic front elevation view of exactly the same subject as in the reference image, straight-on front camera at object height, no perspective distortion, no isometric angle, the whole object and its full base plate visible and centered, $COMMON";;
    side)  echo "Orthographic left side elevation view of exactly the same subject as in the reference image, straight-on side camera at object height, no perspective distortion, no isometric angle, the whole object and its full base plate visible and centered, $COMMON";;
    top)   echo "Orthographic top-down plan view of exactly the same subject as in the reference image, camera looking straight down from directly above, no perspective distortion, the complete base plate footprint clearly visible and centered, $COMMON";;
  esac
}

# ---- 上传参考图 + 提交三视图任务 ----
while IFS=$'\t' read -r id aspect prompt; do
  [ -z "$id" ] && continue
  concept="$PROJ/Assets/Res/$id/picture/concept.png"
  [ -f "$concept" ] || { echo "[skip] $id 无 concept"; continue; }
  need=0
  for v in front side top; do [ -f "$PROJ/Assets/Res/$id/picture/$v.png" ] || need=1; done
  [ "$need" -eq 0 ] && { echo "[skip] $id 三视图已齐"; continue; }

  ref="$ART/refs/$id.jpg"
  [ -f "$ref" ] || ref="$concept"
  if [ ! -f "$STATE/$id.aid" ]; then
    out=$(atlas-skillhub gateway call-tool --service liclick --tool upload_asset --file file_path="$ref" asset_type=image 2>&1)
    aid=$(echo "$out" | grep -oE 'asset_id["'"'"': ]+[A-Za-z0-9_-]+' | head -1 | grep -oE '[A-Za-z0-9_-]+$')
    if [ -z "$aid" ]; then echo "$out" > "$STATE/$id.uploadfail"; echo "[FAIL-upload] $id"; continue; fi
    echo "$aid" > "$STATE/$id.aid"
    echo "[upload] $id -> $aid"
  fi
  aid=$(cat "$STATE/$id.aid")

  for v in front side top; do
    [ -f "$PROJ/Assets/Res/$id/picture/$v.png" ] && continue
    [ -f "$STATE/$id.$v.task" ] && continue
    vp=$(viewprompt "$v")
    args=$(printf '{"prompt":"%s","model":"nano_banana_pro","extra_params":{"aspect_ratio":"%s","image_size":"2K","name":"view_%s_%s","reference_images":[{"asset_id":"%s","type":"image"}]}}' "$vp" "$aspect" "$id" "$v" "$aid")
    out=$(atlas-skillhub gateway call-tool --service liclick --tool generate_image --args "$args" 2>&1)
    tid=$(echo "$out" | grep -oE 'task_id: [0-9a-f-]+' | head -1 | cut -d' ' -f2)
    if [ -n "$tid" ]; then echo "$tid" > "$STATE/$id.$v.task"; echo "[submit] $id/$v -> $tid"
    else
      url=$(echo "$out" | grep -oE 'https://[^"\\ ]+' | head -1)
      if [ -n "$url" ]; then echo "$url" > "$STATE/$id.$v.url"; echo "[fast-done] $id/$v"; else echo "$out" > "$STATE/$id.$v.fail"; echo "[FAIL-submit] $id/$v"; fi
    fi
    sleep 2
  done
done < "$MANIFEST"

# ---- 轮询 + 下载 ----
for round in $(seq 1 60); do
  pending=0
  while IFS=$'\t' read -r id aspect prompt; do
    [ -z "$id" ] && continue
    [ -f "$PROJ/Assets/Res/$id/picture/concept.png" ] || continue
    for v in front side top; do
      dest="$PROJ/Assets/Res/$id/picture/$v.png"
      [ -f "$dest" ] && continue
      [ -f "$STATE/$id.$v.fail" ] && continue
      # 既无任务也无 URL 的视图（上传失败没提交成）不计 pending，避免死等
      [ -f "$STATE/$id.$v.task" ] || [ -f "$STATE/$id.$v.url" ] || continue
      url=""
      [ -f "$STATE/$id.$v.url" ] && url=$(cat "$STATE/$id.$v.url")
      if [ -z "$url" ] && [ -f "$STATE/$id.$v.task" ]; then
        tid=$(cat "$STATE/$id.$v.task")
        out=$(statuscall "$tid")
        if echo "$out" | grep -q '"status": *"Failed"'; then echo "$out" > "$STATE/$id.$v.fail"; echo "[FAIL] $id/$v"; continue; fi
        url=$(echo "$out" | grep -oE 'https://[^"\\ ]+' | head -1)
        [ -n "$url" ] && echo "$url" > "$STATE/$id.$v.url"
      fi
      if [ -n "$url" ]; then
        if curl -sfL -o "$dest" "$url"; then echo "[done] $id/$v"; else rm -f "$STATE/$id.$v.url"; pending=$((pending+1)); fi
      else
        pending=$((pending+1))
      fi
    done
  done < "$MANIFEST"
  [ "$pending" -eq 0 ] && break
  echo "[round $round] pending=$pending"
  sleep 30
done

# ---- 汇总 ----
{
  echo "=== Stage B summary $(date +%H:%M:%S) ==="
  while IFS=$'\t' read -r id aspect prompt; do
    [ -z "$id" ] && continue
    [ -f "$PROJ/Assets/Res/$id/picture/concept.png" ] || { echo "SKIP $id (no concept)"; continue; }
    line="$id:"
    for v in front side top; do
      if [ -f "$PROJ/Assets/Res/$id/picture/$v.png" ]; then line="$line $v=OK"; else line="$line $v=MISS"; fi
    done
    echo "$line"
  done < "$MANIFEST"
} > "$STATE/stageB_summary.txt"
cat "$STATE/stageB_summary.txt"
