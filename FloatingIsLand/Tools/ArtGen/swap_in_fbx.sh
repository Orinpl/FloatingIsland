#!/bin/bash
# ArtGen/fbx_out/<id>.fbx → Assets/Res/<id>/fbx/<id>.fbx
# 只换 .fbx 本体、保留 .fbx.meta（GUID 不变，Prefab 引用不断）；新资产自动建目录。
set -u
PROJ="d:/UnityProject/FloatingIsLand/FloatingIsLand"
OUT="$PROJ/ArtGen/fbx_out"
RES="$PROJ/Assets/Res"

n=0
for src in "$OUT"/*.fbx; do
  [ -f "$src" ] || continue
  id=$(basename "$src" .fbx)
  dst_dir="$RES/$id/fbx"
  mkdir -p "$dst_dir"
  rm -f "$dst_dir/$id.fbx" "$dst_dir/$id.glb"
  cp "$src" "$dst_dir/$id.fbx"
  n=$((n+1))
  echo "[in] $id"
done
echo "swapped $n"
