# -*- coding: utf-8 -*-
"""Blender 内运行：GLB → FBX（内嵌贴图），供 tripo 产物接入 FBX 流水线。

用法:
  blender.exe -b --factory-startup -P convert_glb2fbx.py -- <in.glb> <out.fbx> <asset_id>

做三件事：
1. 导入 GLB（tripo 产物：Color / ORM / NormalGL 三张 jpg，无压缩扩展）
2. 贴图数据块按语义改名：Color→<id>_texture_diffuse、NormalGL→<id>_texture_normal、
   ORM→<id>_texture_orm —— ModelMaterialExtractor 按文件名 Contains(diffuse/normal) 匹配，
   ORM 故意不叫 metallic（通道布局是 R=AO G=Rough B=Metal，直接当 _MetallicGlossMap 挂会错）
3. 解包贴图到临时目录后导出 FBX（embed_textures 需要贴图落盘才能嵌回去）
"""
import os
import sys
import tempfile

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst, asset_id = argv[0], argv[1], argv[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

tmpdir = tempfile.mkdtemp(prefix="glb2fbx_")

for img in list(bpy.data.images):
    name = img.name.lower()
    if name.startswith("color") or "basecolor" in name or "albedo" in name:
        img.name = asset_id + "_texture_diffuse"
    elif "normal" in name:
        img.name = asset_id + "_texture_normal"
    elif name.startswith("orm"):
        img.name = asset_id + "_texture_orm"
    # 落盘：内嵌导出要求图片有实体文件
    ext = ".png"
    fmt = (img.file_format or "").upper()
    if fmt in ("JPEG", "JPG"):
        ext = ".jpg"
    path = os.path.join(tmpdir, img.name + ext)
    img.filepath_raw = path
    try:
        img.save()
    except Exception as e:  # noqa: BLE001 - 贴图存不下来要报出来但不拦其他图
        print(f"[warn] save image {img.name} failed: {e}")

os.makedirs(os.path.dirname(dst), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=dst,
    embed_textures=True,
    path_mode="COPY",
    add_leaf_bones=False,
    bake_anim=False,
)
print(f"[ok] {asset_id}: {src} -> {dst}")
