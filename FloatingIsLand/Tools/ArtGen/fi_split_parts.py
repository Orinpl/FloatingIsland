# -*- coding: utf-8 -*-
"""Blender 内运行：把单网格模型按「连通块空间归组」非破坏性拆成两个节点。

用法:
  blender.exe -b --factory-startup -P fi_split_parts.py -- \
      --fbx <in.fbx> --config <cfg.json> --mode probe|preview|export [--out <path>]

拆分是**纯几何划分**：不重拓扑、不重网格、不动顶点坐标和 UV。做法是先算面的连通分量
（loose parts），按配置的规则把每个连通块整块归到 A 组或 B 组，再用
`mesh.separate(type='SELECTED')` 切成两个对象。每个三角形原样保留，只是换了个父节点。

产物结构刻意保持「静态件当根、活动件当子节点」：
  <原始节点名>            ← 仍是网格节点，名字不变，Unity 那边的 prefab 覆盖才不会失效
    └─ <asset>_<child>    ← 扇叶 / 帆布，原点落在 pivot 上，方便直接旋转

config.json:
{
  "asset_id": "windmill",
  "child_name": "windmill_blade",
  "rule": "cx > 0.09",          # 命中 = 归到 child 组；变量见 RULE_VARS
  "child_pivot": "bbox_center", # bbox_center | origin | [x,y,z]
  "views": [[1,-1,0.6], [0,-1,0], [1,0,0], [0,0,1]]
}
"""
import argparse
import json
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector

RULE_VARS = "cx cy cz sx sy sz minx miny minz maxx maxy maxz faces verts"


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:]
    p = argparse.ArgumentParser()
    p.add_argument("--fbx", required=True)
    p.add_argument("--config", default=None)
    p.add_argument("--mode", default="probe", choices=["probe", "preview", "export"])
    p.add_argument("--out", default=None)
    return p.parse_args(argv)


def load_scene(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if len(meshes) != 1:
        raise SystemExit(f"[err] expected exactly 1 mesh object, got {len(meshes)}: "
                         f"{[o.name for o in meshes]}")
    return meshes[0]


def connected_components(obj):
    """面的连通分量。返回 [(face_index_list, stats_dict), ...]，按面数降序。"""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()

    seen = set()
    comps = []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack = [f]
        seen.add(f.index)
        comp = []
        while stack:
            cur = stack.pop()
            comp.append(cur.index)
            for e in cur.edges:
                for nf in e.link_faces:
                    if nf.index not in seen:
                        seen.add(nf.index)
                        stack.append(nf)
        comps.append(comp)

    mw = obj.matrix_world
    nrm = mw.to_3x3().inverted_safe().transposed()
    out = []
    for idx, comp in enumerate(comps):
        vs = set()
        for fi in comp:
            for v in bm.faces[fi].verts:
                vs.add(v.index)
        pts = [mw @ bm.verts[vi].co for vi in vs]
        mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        ctr = (mn + mx) / 2.0
        size = mx - mn

        # 面法线朝向的面积占比：布片是平的（法线集中在某一轴），杆子是管状（法线铺开）。
        # 位置规则在这个资产上是脆的（连通块在 XYZ 上互相交错），法线分布才分得开。
        area = 0.0
        axis_area = [0.0, 0.0, 0.0]
        for fi in comp:
            f = bm.faces[fi]
            a = f.calc_area()
            area += a
            n = (nrm @ f.normal).normalized()
            for k, comp_n in enumerate((n.x, n.y, n.z)):
                if abs(comp_n) > 0.9:
                    axis_area[k] += a
        inv = 1.0 / area if area > 1e-12 else 0.0
        thin, fat = min(size), max(size)

        out.append((comp, {
            "idx": idx, "faces": len(comp), "verts": len(vs),
            "cx": ctr.x, "cy": ctr.y, "cz": ctr.z,
            "sx": size.x, "sy": size.y, "sz": size.z,
            "minx": mn.x, "miny": mn.y, "minz": mn.z,
            "maxx": mx.x, "maxy": mx.y, "maxz": mx.z,
            "area": area,
            "nxf": axis_area[0] * inv, "nyf": axis_area[1] * inv, "nzf": axis_area[2] * inv,
            "aspect": (thin / fat) if fat > 1e-12 else 0.0,
        }))
    bm.free()
    out.sort(key=lambda c: -c[1]["faces"])
    return out


def classify(comps, rule):
    """按规则把连通块分成 (child_faces, child_stats, base_stats)。"""
    child_faces, child_stats, base_stats = [], [], []
    for faces, st in comps:
        env = dict(st)
        env["math"] = math
        try:
            hit = bool(eval(rule, {"__builtins__": {}}, env))
        except Exception as exc:  # noqa: BLE001 - 规则写错要立刻报出来
            raise SystemExit(f"[err] rule failed on part {st['idx']}: {exc}")
        if hit:
            child_faces.extend(faces)
            child_stats.append(st)
        else:
            base_stats.append(st)
    return child_faces, child_stats, base_stats


def group_bbox(stats):
    if not stats:
        return None, None
    mn = Vector((min(s["minx"] for s in stats),
                 min(s["miny"] for s in stats),
                 min(s["minz"] for s in stats)))
    mx = Vector((max(s["maxx"] for s in stats),
                 max(s["maxy"] for s in stats),
                 max(s["maxz"] for s in stats)))
    return mn, mx


def make_material(name, rgba):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = False
    mat.diffuse_color = rgba
    return mat


def setup_render(out_png, size=640):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.film_transparent = False
    scene.render.filepath = out_png
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'MATERIAL'
    shading.show_object_outline = True
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.05, 0.06, 0.09)


def render_view(objs, direction, out_png, size=640):
    """正交相机沿 direction 看向物体中心，自动框选。"""
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for corner in o.bound_box:
            w = o.matrix_world @ Vector(corner)
            mn = Vector((min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)))
            mx = Vector((max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)))
    center = (mn + mx) / 2.0
    radius = max((mx - mn).length / 2.0, 1e-4)

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = radius * 2.35
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam)

    d = Vector(direction).normalized()
    cam.location = center + d * (radius * 6.0)
    up = Vector((0, 0, 1))
    if abs(d.dot(up)) > 0.999:
        up = Vector((0, 1, 0))
    fwd = -d
    right = fwd.cross(up).normalized()
    trueup = right.cross(fwd).normalized()
    cam.matrix_world = __import__("mathutils").Matrix((
        (right.x, trueup.x, -fwd.x, cam.location.x),
        (right.y, trueup.y, -fwd.y, cam.location.y),
        (right.z, trueup.z, -fwd.z, cam.location.z),
        (0, 0, 0, 1),
    ))

    bpy.context.scene.camera = cam
    setup_render(out_png, size)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)


def do_split(obj, child_faces, child_name, pivot_mode, child_stats):
    """把 child_faces 切成独立对象，父级挂回 obj。返回 (base, child)。"""
    base_name = obj.name
    bpy.context.view_layer.objects.active = obj
    for o in bpy.data.objects:
        o.select_set(False)
    obj.select_set(True)

    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    for f in bm.faces:
        f.select = False
    for v in bm.verts:
        v.select = False
    for e in bm.edges:
        e.select = False
    wanted = set(child_faces)
    for fi in wanted:
        bm.faces[fi].select = True
    bmesh.update_edit_mesh(obj.data)
    bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')

    parts = [o for o in bpy.context.selected_objects if o.type == 'MESH']
    base = obj
    child = [o for o in parts if o is not obj]
    if len(child) != 1:
        raise SystemExit(f"[err] separate produced {len(child)} extra objects")
    child = child[0]

    base.name = base_name
    base.data.name = base_name
    child.name = child_name
    child.data.name = child_name

    # pivot：把子物体原点挪到指定位置，世界坐标下几何完全不动
    if pivot_mode == "bbox_center":
        mn, mx = group_bbox(child_stats)
        target = (mn + mx) / 2.0
    elif isinstance(pivot_mode, (list, tuple)):
        target = Vector(pivot_mode)
    else:
        target = Vector((0, 0, 0))

    if target.length_squared > 0:
        offset = target - child.matrix_world.translation
        child.data.transform(__import__("mathutils").Matrix.Translation(-offset))
        child.matrix_world.translation = target

    child.parent = base
    child.matrix_parent_inverse = base.matrix_world.inverted()
    return base, child


def add_planar_uv2(obj):
    """给子物体加一层平面展开的 UV2，供 FI_SailWind 取风场坐标。

    只加通道、不动几何。tripo 出的网格只有一层 UV，而 FI_SailWind 从 `v.uv2`
    取风场 UV，缺这层的话 mask 恒为 0 —— 帆布挂上 shader 也一动不动。

    展开方式：V 取 Blender 的 Z（高度），U 取 X/Y 里跨度大的那条（布面宽度方向），
    都按包围盒归一化到 0..1。默认 mask 模式 3 靠 U 的两端做固定边，正好对上布面左右缘。
    """
    mesh = obj.data
    mw = obj.matrix_world
    pts = [mw @ v.co for v in mesh.vertices]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    size = mx - mn

    u_axis = 0 if size.x >= size.y else 1
    v_axis = 2
    u_span = max(size[u_axis], 1e-6)
    v_span = max(size[v_axis], 1e-6)

    layer = mesh.uv_layers.get("UV2") or mesh.uv_layers.new(name="UV2")
    for loop in mesh.loops:
        co = mw @ mesh.vertices[loop.vertex_index].co
        layer.data[loop.index].uv = (
            (co[u_axis] - mn[u_axis]) / u_span,
            (co[v_axis] - mn[v_axis]) / v_span,
        )

    print(f"[ok] UV2 on {obj.name}: U=axis{u_axis} span {u_span:.4f}, V=axis{v_axis} span {v_span:.4f}")


def main():
    args = parse_args()
    cfg = json.load(open(args.config, encoding='utf-8')) if args.config else {}
    obj = load_scene(args.fbx)
    comps = connected_components(obj)

    src_polys = len(obj.data.polygons)
    src_verts = len(obj.data.vertices)

    if args.mode == "probe":
        print("###JSON###")
        print(json.dumps({
            "object": obj.name,
            "polys": src_polys, "verts": src_verts,
            "materials": [m.name if m else None for m in obj.data.materials],
            "uv_layers": [uv.name for uv in obj.data.uv_layers],
            "loose_parts": len(comps),
            "parts": [{k: (round(v, 4) if isinstance(v, float) else v)
                       for k, v in st.items()} for _, st in comps],
        }, indent=1))
        return

    rule = cfg["rule"]
    child_faces, child_stats, base_stats = classify(comps, rule)
    print(f"[info] rule={rule!r}")
    print(f"[info] child parts={len(child_stats)} faces={len(child_faces)} | "
          f"base parts={len(base_stats)} faces={src_polys - len(child_faces)}")
    if not child_stats or not base_stats:
        raise SystemExit("[err] rule put everything in one group")

    if args.mode == "preview":
        red = make_material("GRP_child", (0.90, 0.15, 0.12, 1))
        blue = make_material("GRP_base", (0.30, 0.55, 0.95, 1))
        obj.data.materials.clear()
        obj.data.materials.append(blue)
        obj.data.materials.append(red)
        wanted = set(child_faces)
        for poly in obj.data.polygons:
            poly.material_index = 1 if poly.index in wanted else 0
        views = cfg.get("views", [[1, -1, 0.6], [0, -1, 0], [1, 0, 0], [0, 0, 1]])
        for i, d in enumerate(views):
            render_view([obj], d, f"{args.out}_v{i}.png")
        print(f"[ok] preview -> {args.out}_v*.png")
        return

    base, child = do_split(obj, child_faces, cfg["child_name"],
                           cfg.get("child_pivot", "bbox_center"), child_stats)

    if cfg.get("child_uv2"):
        add_planar_uv2(child)

    out_polys = len(base.data.polygons) + len(child.data.polygons)
    out_verts = len(base.data.vertices) + len(child.data.vertices)
    print(f"[verify] polys {src_polys} -> {out_polys} "
          f"(base {len(base.data.polygons)} + child {len(child.data.polygons)})")
    print(f"[verify] verts {src_verts} -> {out_verts} "
          f"(separate 会在切缝处复制共享顶点，verts 只增不减)")
    if out_polys != src_polys:
        raise SystemExit(f"[err] polygon count changed: {src_polys} -> {out_polys}")
    for o in (base, child):
        if not o.data.uv_layers:
            raise SystemExit(f"[err] {o.name} lost its UV layer")

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=args.out,
        embed_textures=True,
        path_mode="COPY",
        add_leaf_bones=False,
        bake_anim=False,
    )
    print(f"[ok] export -> {args.out}")
    print(f"[ok] hierarchy: {base.name} > {child.name} "
          f"(child origin @ {[round(v, 4) for v in child.matrix_world.translation]})")


main()
