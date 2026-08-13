#!/bin/bash
# ArtGen/gen3d/*.glb → ArtGen/fbx_out/<id>.fbx（Blender 无头转换，内嵌贴图）
# 幂等：已有产物跳过。转完打印大小对照，异常（fbx 缺失/过小）标记 FAIL。
set -u
PROJ="d:/UnityProject/FloatingIsLand/FloatingIsLand"
GEN="$PROJ/ArtGen/gen3d"
OUT="$PROJ/ArtGen/fbx_out"
BLENDER=$(ls -d /d/tools/blender-*/blender.exe 2>/dev/null | head -1)
[ -z "$BLENDER" ] && { echo "no blender"; exit 1; }
mkdir -p "$OUT"

fail=0
for glb in "$GEN"/*.glb; do
  [ -f "$glb" ] || continue
  id=$(basename "$glb" .glb)
  dst="$OUT/$id.fbx"
  [ -f "$dst" ] && { echo "[skip] $id"; continue; }
  "$BLENDER" -b --factory-startup -P "$PROJ/Tools/ArtGen/convert_glb2fbx.py" -- "$glb" "$dst" "$id" >"$OUT/$id.log" 2>&1
  if [ -f "$dst" ] && [ "$(stat -c%s "$dst")" -gt 100000 ]; then
    echo "[ok] $id  glb=$(stat -c%s "$glb")  fbx=$(stat -c%s "$dst")"
  else
    echo "[FAIL] $id （见 $OUT/$id.log）"
    fail=$((fail+1))
  fi
done
echo "done, fail=$fail"
