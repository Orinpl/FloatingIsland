#!/bin/bash
# 阶段A：按 manifest.tsv 批量生成效果图（nano_banana_pro），下载到 Assets/Res/<id>/picture/concept.png
# 状态落在 ArtGen/state/<id>.{task,url,fail}，全部结束写 stageA_summary.txt
set -u
PROJ="d:/UnityProject/FloatingIsLand/FloatingIsLand"
ART="$PROJ/ArtGen"          # 中间产物目录（大文件，仓库忽略）
SCRIPTDIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STATE="$ART/state"
MANIFEST="$SCRIPTDIR/manifest.tsv"
STYLE=", single subject centered, plain very light blue-grey background, low poly 3D game art, faceted geometry, flat shading, soft pastel colors, clean silhouette, no text, no watermark, style similar to ISLANDERS game concept art"
mkdir -p "$STATE"

# 新下载的概念图存 .png，入库时统一转成 .jpg 省体积；只认 .png 的话已入库的资产会被判成
# "没有概念图"，整批重新生成一遍——白烧额度，还会把已定稿的美术换成另一张图
findpic() { # $1=id $2=名字 → 打印路径；找不到返回非 0
  local base="$PROJ/Assets/Res/$1/picture/$2"
  if [ -f "$base.png" ]; then echo "$base.png"; return 0; fi
  if [ -f "$base.jpg" ]; then echo "$base.jpg"; return 0; fi
  return 1
}

call() { atlas-skillhub gateway call-tool --service liclick --tool "$1" ${2:+--args "$2"} 2>&1; }
statuscall() { atlas-skillhub gateway call-tool --service liclick --tool get_task_status task_id="$1" task_type=image 2>&1; }

# ---- 提交 ----
while IFS=$'\t' read -r id aspect prompt; do
  [ -z "$id" ] && continue
  findpic "$id" concept >/dev/null && { echo "[skip] $id 已有 concept"; continue; }
  [ -f "$STATE/$id.task" ] && { echo "[skip] $id 已提交"; continue; }
  args=$(printf '{"prompt":"%s","model":"nano_banana_pro","extra_params":{"aspect_ratio":"%s","image_size":"2K","name":"concept_%s"}}' "$prompt$STYLE" "$aspect" "$id")
  out=$(call generate_image "$args")
  tid=$(echo "$out" | grep -oE 'task_id: [0-9a-f-]+' | head -1 | cut -d' ' -f2)
  if [ -n "$tid" ]; then
    echo "$tid" > "$STATE/$id.task"
    echo "[submit] $id -> $tid"
  else
    # 可能 2 分钟内直接完成，尝试取 URL
    url=$(echo "$out" | grep -oE 'https://[^"\\ ]+' | head -1)
    if [ -n "$url" ]; then echo "$url" > "$STATE/$id.url"; echo "[fast-done] $id"; else echo "$out" > "$STATE/$id.fail"; echo "[FAIL-submit] $id"; fi
  fi
  sleep 2
done < "$MANIFEST"

# ---- 轮询 + 下载 ----
for round in $(seq 1 60); do
  pending=0
  while IFS=$'\t' read -r id aspect prompt; do
    [ -z "$id" ] && continue
    dest="$PROJ/Assets/Res/$id/picture/concept.png"
    findpic "$id" concept >/dev/null && continue
    [ -f "$STATE/$id.fail" ] && continue
    url=""
    [ -f "$STATE/$id.url" ] && url=$(cat "$STATE/$id.url")
    if [ -z "$url" ] && [ -f "$STATE/$id.task" ]; then
      tid=$(cat "$STATE/$id.task")
      out=$(statuscall "$tid")
      if echo "$out" | grep -q '"status": *"Failed"'; then echo "$out" > "$STATE/$id.fail"; echo "[FAIL] $id"; continue; fi
      url=$(echo "$out" | grep -oE 'https://[^"\\ ]+' | head -1)
      [ -n "$url" ] && echo "$url" > "$STATE/$id.url"
    fi
    if [ -n "$url" ]; then
      mkdir -p "$PROJ/Assets/Res/$id/picture"
      if curl -sfL -o "$dest" "$url"; then echo "[done] $id"; else rm -f "$STATE/$id.url"; pending=$((pending+1)); fi
    else
      pending=$((pending+1))
    fi
  done < "$MANIFEST"
  [ "$pending" -eq 0 ] && break
  echo "[round $round] pending=$pending"
  sleep 30
done

# ---- 汇总 ----
{
  echo "=== Stage A summary $(date +%H:%M:%S) ==="
  while IFS=$'\t' read -r id aspect prompt; do
    [ -z "$id" ] && continue
    if findpic "$id" concept >/dev/null; then echo "OK   $id"
    elif [ -f "$STATE/$id.fail" ]; then echo "FAIL $id"
    else echo "TIMEOUT $id"; fi
  done < "$MANIFEST"
} > "$STATE/stageA_summary.txt"
cat "$STATE/stageA_summary.txt"
