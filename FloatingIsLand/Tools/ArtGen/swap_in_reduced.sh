#!/bin/bash
# ArtGen/reduced/<id>.fbx（低模拓扑产物）→ Assets/Res/<id>/fbx/<id>.fbx
# 同时清掉上一轮提取的 <id>_texture_*（新 FBX 自带 tripo_node_*_BaseColor 等重烘贴图，
# 两套同时在 mat/ 里会让提取器的语义匹配二义）。保留 .fbx.meta 与 <id>.mat（GUID 稳定）。
set -u
PROJ="d:/UnityProject/FloatingIsLand/FloatingIsLand"
SRC="$PROJ/ArtGen/reduced"
RES="$PROJ/Assets/Res"

n=0
for f in "$SRC"/*.fbx; do
  [ -f "$f" ] || continue
  id=$(basename "$f" .fbx)
  dst="$RES/$id/fbx"
  mkdir -p "$dst"
  rm -f "$dst/$id.fbx"
  cp "$f" "$dst/$id.fbx"
  # 清上一轮语义贴图（本轮换成 tripo_node_* 命名）
  rm -f "$RES/$id/mat/${id}_texture_"*.jpg "$RES/$id/mat/${id}_texture_"*.jpg.meta \
        "$RES/$id/mat/${id}_texture_"*.png "$RES/$id/mat/${id}_texture_"*.png.meta 2>/dev/null
  n=$((n+1))
  echo "[in] $id"
done
echo "swapped $n"
