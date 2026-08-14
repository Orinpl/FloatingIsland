# -*- coding: utf-8 -*-
"""Blender 内运行：GLB → FBX（内嵌贴图），供生模产物接入 FBX 流水线。

用法:
  blender.exe -b --factory-startup -P convert_glb2fbx.py -- <in.glb> <out.fbx> <asset_id>

做三件事：
1. 导入 GLB
2. 贴图数据块按语义改名：diffuse→<id>_texture_diffuse、normal→<id>_texture_normal、
   orm→<id>_texture_orm —— ModelMaterialExtractor 按文件名 Contains(diffuse/normal) 匹配，
   ORM 故意不叫 metallic（通道布局是 R=AO G=Rough B=Metal，直接当 _MetallicGlossMap 挂会错）
3. 解包贴图到临时目录后导出 FBX（embed_textures 需要贴图落盘才能嵌回去）

语义从材质连线取，不从图片名字猜。名字是生成方给的，换一家模型服务就换一套：
tripo 出 Color / NormalGL / ORM，hunyuan 出 texture_pbr_<日期> 这种毫无语义的名字。
按名字匹配的老写法对后者三个分支全不中，贴图就原样漏了过去 —— dock（风力飞船）
就是这么带着 texture_pbr_20250901 这个名字进的库，从此每一步自动化都得手工绕开它。
连线关系由 glTF 规范定死，与命名无关，所以按连线判角色才跨服务商稳定。
"""
import os
import sys
import tempfile

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst, asset_id = argv[0], argv[1], argv[2]


def trace_image(socket):
    """从一个输入插槽顺着连线回溯到最终的 Image Texture。

    不能只看直连的上一个节点：glTF 导入后 Normal 插槽前面必定隔着一个 Normal Map
    节点，ORM 这类打包贴图前面还会有 Separate Color。
    """
    if socket is None or not socket.is_linked:
        return None

    stack = [socket.links[0].from_node]
    seen = set()
    while stack:
        node = stack.pop()
        if node.name in seen:
            continue
        seen.add(node.name)
        if node.type == "TEX_IMAGE":
            return node.image
        for socket_in in node.inputs:
            if socket_in.is_linked:
                stack.append(socket_in.links[0].from_node)
    return None


def roles_from_materials():
    """图片 → 语义，取自 Principled BSDF 的插槽连线。"""
    roles = {}
    for material in bpy.data.materials:
        if not material.use_nodes or material.node_tree is None:
            continue

        bsdf = None
        for node in material.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                bsdf = node
                break
        if bsdf is None:
            continue

        # Metallic 与 Roughness 在 glTF 里同源，都会回溯到那张 ORM 打包图。
        for socket_name, role in (
            ("Base Color", "diffuse"),
            ("Normal", "normal"),
            ("Metallic", "orm"),
            ("Roughness", "orm"),
        ):
            image = trace_image(bsdf.inputs.get(socket_name))
            if image is not None:
                roles.setdefault(image.name, role)
    return roles


def role_from_name(name):
    """连线取不到语义时的兜底：仍按 tripo 的固定命名猜一把。"""
    name = name.lower()
    if name.startswith("color") or "basecolor" in name or "albedo" in name:
        return "diffuse"
    if "normal" in name:
        return "normal"
    if name.startswith("orm"):
        return "orm"
    return None


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

tmpdir = tempfile.mkdtemp(prefix="glb2fbx_")
roles = roles_from_materials()
renamed = {}

for img in list(bpy.data.images):
    role = roles.get(img.name) or role_from_name(img.name)
    if role is not None:
        img.name = asset_id + "_texture_" + role
        renamed[role] = img.name

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

# 认不出 diffuse 就必须喊出来。这一步静默失败的代价是整条链路都跟着歪：
# 贴图名不合约定 → Unity 按贴图名派生的材质槽名也不合约定 → 提取器匹配不到
# diffuse → 白模。而这一切在导出时看起来是成功的。
if "diffuse" not in renamed:
    print(f"[err] {asset_id}: 认不出 base color 贴图，"
          f"图片有 {[i.name for i in bpy.data.images]}，"
          f"下游会当成白模处理，请检查 GLB 的材质连线")

os.makedirs(os.path.dirname(dst), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=dst,
    embed_textures=True,
    path_mode="COPY",
    add_leaf_bones=False,
    bake_anim=False,
)
print(f"[ok] {asset_id}: {src} -> {dst}  贴图 {renamed}")
