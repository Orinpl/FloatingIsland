#!/bin/bash
# tripo-v3.1 概念图三视图 → 3D 模型。与 run_stage_c.sh 同族，但：
#   - 输入是新概念图切出的视图（ArtGen/views/<id>.front.jpg + .side.jpg），manifest 由工作流生成
#   - side 视图按 manifest 第 3 列映射到 tripo 的 left/right 槽（tripo 不支持 top 槽，top 仅存档）
#   - 产物先下到 ArtGen/gen3d/<id>.<ext> 暂存，不直接覆盖 Assets/Res（换入由后续步骤统一做）
# manifest: ArtGen/state/tripo_manifest.tsv  （TAB 分隔：id \t has_side(0/1) \t side_slot(left/right) \t prompt）
set -u
PROJ="d:/UnityProject/FloatingIsLand/FloatingIsLand"
ART="$PROJ/ArtGen"
STATE="$ART/state"
GEN="$ART/gen3d"
MANIFEST="$STATE/tripo_manifest.tsv"
mkdir -p "$STATE" "$GEN"

statuscall() { atlas-skillhub gateway call-tool --service liclick --tool get_task_status task_id="$1" task_type=model_3d 2>&1; }

uploadview() { # $1=id $2=view → echo asset_id（带缓存）
  local f="$ART/views/$1.$2.jpg" cache="$STATE/$1.$2.vaid"
  [ -f "$cache" ] && { cat "$cache"; return 0; }
  [ -f "$f" ] || return 1
  local out aid
  out=$(atlas-skillhub gateway call-tool --service liclick --tool upload_asset --file file_path="$f" asset_type=image 2>&1)
  aid=$(echo "$out" | grep -oE 'asset_id["'"'"': ]+[A-Za-z0-9_-]+' | head -1 | grep -oE '[A-Za-z0-9_-]+$')
  [ -n "$aid" ] && echo "$aid" > "$cache" && echo "$aid"
}

# ---- 提交 ----
while IFS=$'\t' read -r id has_side side_slot prompt; do
  [ -z "$id" ] && continue
  ls "$GEN/$id".* >/dev/null 2>&1 && { echo "[skip] $id 已有产物"; continue; }
  [ -f "$STATE/$id.3d.task" ] && { echo "[skip] $id 已提交"; continue; }
  [ -f "$STATE/$id.3d.fail" ] && { echo "[skip] $id 曾失败（删 state/$id.3d.fail 重试）"; continue; }

  fa=$(uploadview "$id" front) || { echo "[FAIL-view] $id 无 front"; continue; }
  [ -z "$fa" ] && { echo "[FAIL-upload] $id front"; echo "upload front failed" > "$STATE/$id.3d.fail"; continue; }
  side_json=""
  if [ "$has_side" = "1" ]; then
    sa=$(uploadview "$id" side)
    if [ -n "$sa" ]; then side_json=",\"$side_slot\":\"asset:$sa\""; else echo "[warn] $id side 上传失败，仅用 front"; fi
  fi

  # 2026-08-13 用户指示：不加低多边形限制、不带风格提示词，纯按三视图生成（无 prompt、无 face_count）
  args="{\"request_type\":\"generation\",\"model\":\"tripo-v3.1\",\"front\":\"asset:$fa\"$side_json}"
  out=$(atlas-skillhub gateway call-tool --service liclick --tool generate_model_3d --args "$args" 2>&1)
  tid=$(echo "$out" | grep -oE 'task_id[": ]+[0-9a-f-]+' | head -1 | grep -oE '[0-9a-f-]{8,}')
  if [ -n "$tid" ]; then echo "$tid" > "$STATE/$id.3d.task"; echo "[submit] $id -> $tid"
  else
    url=$(echo "$out" | grep -oE 'https://[^"\\ ]+\.(fbx|glb|obj|stl|usdz|zip)[^"\\ ]*' | head -1)
    if [ -n "$url" ]; then echo "$url" > "$STATE/$id.3d.url"; echo "[fast-done] $id"; else echo "$out" > "$STATE/$id.3d.fail"; echo "[FAIL-submit] $id"; fi
  fi
  sleep 2
done < "$MANIFEST"

# ---- 轮询 + 下载（60s x 40 轮）----
for round in $(seq 1 40); do
  pending=0
  while IFS=$'\t' read -r id has_side side_slot prompt; do
    [ -z "$id" ] && continue
    ls "$GEN/$id".* >/dev/null 2>&1 && continue
    [ -f "$STATE/$id.3d.fail" ] && continue
    [ -f "$STATE/$id.3d.task" ] || [ -f "$STATE/$id.3d.url" ] || continue
    url=""
    [ -f "$STATE/$id.3d.url" ] && url=$(cat "$STATE/$id.3d.url")
    if [ -z "$url" ]; then
      tid=$(cat "$STATE/$id.3d.task")
      out=$(statuscall "$tid")
      if echo "$out" | grep -q '"status": *"Failed"'; then echo "$out" > "$STATE/$id.3d.fail"; echo "[FAIL] $id"; continue; fi
      # 只认模型扩展名 URL；Processing 阶段响应里就有 thumbnail jpg，不能拿第一个 https
      url=$(echo "$out" | grep -oE 'https://[^"\\ ]+\.(fbx|glb|obj|stl|usdz|zip)[^"\\ ]*' | head -1)
      [ -n "$url" ] && echo "$url" > "$STATE/$id.3d.url"
      # 顺手存缩略图 URL（后续 reduce_face/texture 任务要用 model_image）
      if [ ! -f "$STATE/$id.3d.thumb" ]; then
        thumb=$(echo "$out" | grep -oE 'https://[^"\\ ]+\.(jpg|jpeg|png|webp)[^"\\ ]*' | head -1)
        [ -n "$thumb" ] && echo "$thumb" > "$STATE/$id.3d.thumb"
      fi
    fi
    if [ -n "$url" ]; then
      ext="${url##*.}"; ext="${ext%%\?*}"
      case "$ext" in fbx|glb|obj|stl|usdz|zip) ;; *) ext="glb" ;; esac
      if curl -sfL -o "$GEN/$id.$ext" "$url"; then echo "[done] $id ($ext)"; else rm -f "$STATE/$id.3d.url"; pending=$((pending+1)); fi
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
  echo "=== tripo gen summary $(date +%H:%M:%S) ==="
  while IFS=$'\t' read -r id has_side side_slot prompt; do
    [ -z "$id" ] && continue
    if ls "$GEN/$id".* >/dev/null 2>&1; then echo "OK   $id  $(ls "$GEN/$id".* | head -1)"
    elif [ -f "$STATE/$id.3d.fail" ]; then echo "FAIL $id"
    else echo "MISS $id"; fi
  done < "$MANIFEST"
} > "$STATE/tripo_summary.txt"
cat "$STATE/tripo_summary.txt"
