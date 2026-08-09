# -*- coding: utf-8 -*-
"""按配表 footprint 程序化渲染「地基引导图」，喂给 stage A 当结构底稿。

## 为什么需要它

异形占地（L 形 / 凹形）光靠提示词描述格子数是**不可靠的**。实测（2026-08-09，nano_banana_pro）：

    只写文字描述 L 形                     → 出 3 块 V 形（照抄参考图）
    + 一张俯视平面引导图                  → 出 4 块，但排成 2×2
    + 等轴测地基引导图 + 原 concept.jpg   → 又回到 3 块 V 形
    只给等轴测地基引导图，去掉 concept    → ✅ 正确

结论两条：

1. **原 concept 当风格参考是有害的。** 只要它在 reference_images 里，构图就会被拽回旧样，
   文字里写多少遍「exactly four tiles」都没用。风格靠 run_stage_a.sh 里那串 STYLE 常量就够了。
2. **把「布局生成」降级成「编辑」。** 先用代码把地基渲出来——形状来自配表，不可能错——
   再让模型只做「往这块地基上加房子」。

这个脚本负责第 2 条的前半截。

## 用法

    python3 Tools/ArtGen/make_plate_guide.py                    # 全部有 footprint 的资产
    python3 Tools/ArtGen/make_plate_guide.py residence_02 ...   # 只做指定的

产物在 ArtGen/guides/<id>.png（等轴测）与 ArtGen/guides/<id>.plan.png（俯视，排查用）。
拿等轴测那张 upload_asset，配一段「这张图就是最终地基，只许往上加建筑，不许改轮廓」的提示词。

掩码行序与 Footprint.cs 一致：第 0 行贴 +Z（远端），最后一行贴 z=0（近端）。
"""
import json
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TABLES = os.path.join(ROOT, "Assets", "Resources", "Tables")
OUTDIR = os.path.join(ROOT, "ArtGen", "guides")

GRASS = (133, 186, 84)
RIM = (226, 214, 193)
SIDE = (198, 183, 160)
SIDE_DARK = (176, 161, 139)
BG = (236, 239, 241)


def load_footprints():
    """资产 id → 掩码。建筑按 variantId，元素按 elementId，与 manifest.tsv 的 id 对齐。"""
    out = {}
    for table, key in (("BuildingVariant", "variantId"), ("MapElement", "elementId")):
        path = os.path.join(TABLES, table + ".json")
        if not os.path.exists(path):
            continue
        with open(path, encoding="utf-8") as f:
            for row in json.load(f):
                mask = row.get("footprint")
                if mask:
                    out[row[key]] = mask
    return out


def render_plan(mask, path, cell=300, pad=90, gap=10):
    """俯视平面图：只用来肉眼核对掩码，不建议直接喂给模型——平面图它读不懂透视关系。"""
    rows, cols = len(mask), len(mask[0])
    img = Image.new("RGB", (cols * cell + pad * 2, rows * cell + pad * 2), BG)
    d = ImageDraw.Draw(img)
    for r, line in enumerate(mask):
        for c, ch in enumerate(line):
            if ch != "#":
                continue
            x0, y0 = pad + c * cell + gap, pad + r * cell + gap
            x1, y1 = pad + (c + 1) * cell - gap, pad + (r + 1) * cell - gap
            d.rounded_rectangle([x0, y0, x1, y1], radius=34, fill=RIM)
            d.rounded_rectangle([x0 + 16, y0 + 16, x1 - 16, y1 - 16], radius=24, fill=GRASS)
    img.save(path)


def render_iso(mask, path, tw=260, th=150, thick=46, pad=110):
    """等轴测地基：这张才是喂给模型的底稿，相机角度与 STYLE 里要求的 isometric 一致。"""
    rows, cols = len(mask), len(mask[0])
    occ = {(u, v) for v, line in enumerate(mask) for u, ch in enumerate(line) if ch == "#"}
    if not occ:
        raise ValueError("掩码没有任何 '#'")

    def project(a, b):
        return ((a - b) * tw / 2.0, (a + b) * th / 2.0)

    pts = [project(a, b) for a in range(cols + 1) for b in range(rows + 1)]
    min_x, max_x = min(p[0] for p in pts), max(p[0] for p in pts)
    min_y, max_y = min(p[1] for p in pts), max(p[1] for p in pts)
    img = Image.new("RGB",
                    (int(max_x - min_x) + pad * 2, int(max_y - min_y) + pad * 2 + thick), BG)
    d = ImageDraw.Draw(img)

    def off(p):
        return (p[0] - min_x + pad, p[1] - min_y + pad)

    # 画序：按 u+v 从小到大，保证近处压住远处
    order = sorted(occ, key=lambda t: t[0] + t[1])

    for u, v in order:
        b, c, e = off(project(u + 1, v)), off(project(u + 1, v + 1)), off(project(u, v + 1))
        # 侧面只在轮廓边挤出，内部相邻处不画，否则地基会看起来是散块拼的
        if (u, v + 1) not in occ:
            d.polygon([e, c, (c[0], c[1] + thick), (e[0], e[1] + thick)], fill=SIDE_DARK)
        if (u + 1, v) not in occ:
            d.polygon([c, b, (b[0], b[1] + thick), (c[0], c[1] + thick)], fill=SIDE)

    for u, v in order:
        a, b = off(project(u, v)), off(project(u + 1, v))
        c, e = off(project(u + 1, v + 1)), off(project(u, v + 1))
        d.polygon([a, b, c, e], fill=RIM)
        k = 0.11  # 石边宽度占格子的比例
        ia = (a[0] + (c[0] - a[0]) * k, a[1] + (c[1] - a[1]) * k)
        ic = (c[0] + (a[0] - c[0]) * k, c[1] + (a[1] - c[1]) * k)
        ib = (b[0] + (e[0] - b[0]) * k, b[1] + (e[1] - b[1]) * k)
        ie = (e[0] + (b[0] - e[0]) * k, e[1] + (b[1] - e[1]) * k)
        d.polygon([ia, ib, ic, ie], fill=GRASS)

    img.save(path)


def main():
    footprints = load_footprints()
    wanted = sys.argv[1:] or sorted(footprints)
    os.makedirs(OUTDIR, exist_ok=True)

    missing = [a for a in wanted if a not in footprints]
    for a in missing:
        print("[skip] %s 配表里没有 footprint" % a)

    for asset in [a for a in wanted if a in footprints]:
        mask = footprints[asset]
        iso = os.path.join(OUTDIR, asset + ".png")
        render_iso(mask, iso)
        render_plan(mask, os.path.join(OUTDIR, asset + ".plan.png"))
        solid = sum(line.count("#") for line in mask)
        print("%-18s %-24s %d 列 × %d 行，%d 格 → %s"
              % (asset, "|".join(mask), len(mask[0]), len(mask), solid, iso))


if __name__ == "__main__":
    main()
