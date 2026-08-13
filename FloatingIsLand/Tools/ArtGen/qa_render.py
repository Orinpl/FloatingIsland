# -*- coding: utf-8 -*-
"""Blender 无头质检渲染：按清单逐个导入模型（fbx/glb），Workbench+贴图模式渲两个视角。
用法: blender -b --factory-startup -noaudio -P qa_render.py -- <list.tsv> <outdir>
list.tsv 每行: name<TAB>path   输出: <outdir>/<name>_a.png / _b.png
"""
import math
import os
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:]
listfile, outdir = argv[0], argv[1]
os.makedirs(outdir, exist_ok=True)

jobs = []
for line in open(listfile, encoding="utf-8"):
    line = line.strip()
    if line:
        name, path = line.split("\t")
        jobs.append((name, path))

for name, path in jobs:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ext = path.lower().rsplit(".", 1)[-1]
    try:
        if ext == "fbx":
            bpy.ops.import_scene.fbx(filepath=path)
        else:
            bpy.ops.import_scene.gltf(filepath=path)
    except Exception as e:  # noqa: BLE001
        print(f"IMPORT-FAIL\t{name}\t{e}", flush=True)
        continue

    mn = mathutils.Vector((1e18, 1e18, 1e18))
    mx = mathutils.Vector((-1e18, -1e18, -1e18))
    nmesh = 0
    for ob in bpy.context.scene.objects:
        if ob.type != "MESH":
            continue
        nmesh += 1
        for c in ob.bound_box:
            w = ob.matrix_world @ mathutils.Vector(c)
            mn = mathutils.Vector((min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)))
            mx = mathutils.Vector((max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)))
    if nmesh == 0:
        print(f"NO-MESH\t{name}", flush=True)
        continue
    center = (mn + mx) / 2
    diag = (mx - mn).length or 1.0

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = 384
    scene.render.resolution_y = 384
    scene.render.film_transparent = False

    camd = bpy.data.cameras.new("qacam")
    camd.lens = 40
    cam = bpy.data.objects.new("qacam", camd)
    scene.collection.objects.link(cam)
    scene.camera = cam

    el = math.radians(22)
    dist = diag * 1.25
    for tag, yaw_deg in (("a", 35), ("b", 145)):
        yaw = math.radians(yaw_deg)
        off = mathutils.Vector((
            math.cos(yaw) * math.cos(el),
            math.sin(yaw) * math.cos(el),
            math.sin(el),
        )) * dist
        cam.location = center + off
        direction = center - cam.location
        cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(outdir, f"{name}_{tag}.png")
        bpy.ops.render.render(write_still=True)
        print(f"RENDERED\t{name}\t{tag}", flush=True)

print("=== qa render done ===", flush=True)
