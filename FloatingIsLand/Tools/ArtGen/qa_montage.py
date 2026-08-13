# -*- coding: utf-8 -*-
"""把 qa_render 的输出拼成对比大图：每行一个资产 [原a|原b|减a|减b]，6 行一张。
用法: python3 qa_montage.py <qadir> <ids逗号分隔> <out前缀>
"""
import os
import sys

from PIL import Image, ImageDraw

qadir, ids_csv, prefix = sys.argv[1], sys.argv[2], sys.argv[3]
ids = [i for i in ids_csv.split(",") if i]
TILE = 384
LABEL = 22
COLS = ["orig_a", "orig_b", "red_a", "red_b"]

rows_per_sheet = 6
for si in range(0, len(ids), rows_per_sheet):
    chunk = ids[si:si + rows_per_sheet]
    sheet = Image.new("RGB", (TILE * 4, (TILE + LABEL) * len(chunk)), (30, 30, 30))
    d = ImageDraw.Draw(sheet)
    for r, aid in enumerate(chunk):
        y0 = r * (TILE + LABEL)
        d.text((6, y0 + 4), f"{aid}   [orig-a | orig-b | reduced-a | reduced-b]", fill=(255, 255, 0))
        for c, col in enumerate(COLS):
            p = os.path.join(qadir, f"{aid}_{col}.png")
            if os.path.isfile(p):
                sheet.paste(Image.open(p).convert("RGB"), (c * TILE, y0 + LABEL))
            else:
                d.text((c * TILE + 10, y0 + LABEL + 10), "MISSING", fill=(255, 0, 0))
    out = f"{prefix}_{si // rows_per_sheet + 1}.png"
    sheet.save(out)
    print(out, flush=True)
