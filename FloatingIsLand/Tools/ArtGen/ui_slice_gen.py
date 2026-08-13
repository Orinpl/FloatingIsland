# -*- coding: utf-8 -*-
"""把 Assets/ArtRes/UI 的 7 张原画（3508x2480，画面只占中间一小块）加工成
Unity 可直接用的九宫格 Sprite，输出到 Assets/Resources/UI/。

三步：
1. 按 alpha 包围盒裁掉大片空白；
2. 预乘 alpha 后降采样（直接 resize RGBA 会在边缘出黑边／白边）；
3. 连 .meta 一起写，把 textureType=Sprite、spriteMode=Single、spriteBorder 定死，
   免得 Unity 按默认规则导入成 Default 贴图（那样 Image 根本挂不上）。

border 取值原则：切在「端头造型结束、中段变平」的位置，同时保证
border 之和小于游戏里最小的按钮尺寸（最矮 52px、最窄 150px），
否则 Unity 九宫格会让上下（左右）边压在一起，糊成一团。
"""
import hashlib
import os
import tempfile
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Assets", "ArtRes", "UI")
DST = os.path.join(ROOT, "Assets", "Resources", "UI")
# 预览图只是给人看 border 切得对不对的中间产物，别落进工程里
PREVIEW = tempfile.gettempdir()

# (源文件, 输出名, 输出宽, border=(左,下,右,上), 说明)
SPECS = [
    ("4.png", "btn_primary",   384, (40, 26, 40, 14), "主按钮：橙色木牌"),
    ("3.png", "btn_secondary", 384, (36, 18, 36, 10), "次按钮：浅色木条"),
    ("7.png", "btn_icon",      176, (26, 30, 30, 20), "小方按钮：橙色方块"),
    ("5.png", "panel_bg",      384, (40, 40, 40, 30), "面板/卡片底：米色块"),
    ("6.png", "card_header",   192, (28, 30, 28, 118), "带橙色标题条的卡片底"),
    ("1.png", "frame_wood",    512, (90, 78, 80, 70), "木框：四角交叉木条"),
    ("2.png", "frame_rope",    512, (80, 80, 80, 70), "木框：绑绳款"),
]

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {bl}, y: {bb}, z: {br}, w: {bt}}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_for(key):
    """稳定 GUID：同名再跑一次结果不变，重生成不会打断已有引用。"""
    return hashlib.md5(("FloatingIsLand/UI/" + key).encode("utf-8")).hexdigest()


def load_cropped(name):
    im = Image.open(os.path.join(SRC, name)).convert("RGBA")
    return im.crop(im.getchannel("A").getbbox())


def downscale(im, width):
    h = max(1, round(im.height * width / im.width))
    # RGBa = 预乘 alpha；不预乘的话透明区的 RGB 会被插值带进不透明边缘
    return im.convert("RGBa").resize((width, h), Image.LANCZOS).convert("RGBA")


def nine_slice(sprite, border, size):
    """PIL 版九宫格，只用来出预览图，验证 border 切得对不对。"""
    bl, bb, br, bt = border
    sw, sh = sprite.size
    tw, th = size
    xs = [(0, bl), (bl, sw - br), (sw - br, sw)]
    ys = [(0, bt), (bt, sh - bb), (sh - bb, sh)]
    dxs = [(0, bl), (bl, tw - br), (tw - br, tw)]
    dys = [(0, bt), (bt, th - bb), (th - bb, th)]
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    for (y0, y1), (dy0, dy1) in zip(ys, dys):
        for (x0, x1), (dx0, dx1) in zip(xs, dxs):
            if dx1 <= dx0 or dy1 <= dy0:
                continue
            part = sprite.crop((x0, y0, x1, y1))
            part = part.convert("RGBa").resize((dx1 - dx0, dy1 - dy0), Image.BILINEAR).convert("RGBA")
            out.paste(part, (dx0, dy0))
    return out


os.makedirs(DST, exist_ok=True)
meta_path = DST + ".meta"
if not os.path.exists(meta_path):
    with open(meta_path, "w", newline="\n") as f:
        f.write(FOLDER_META.format(guid=guid_for("folder")))

# 游戏里实际用到的按钮尺寸，用来出拉伸预览
TEST_RECTS = [(300, 52), (324, 62), (320, 72), (150, 92), (760, 76), (150, 160), (360, 230)]

sheet_rows = []
for src, name, width, border, desc in SPECS:
    im = downscale(load_cropped(src), width)
    bl, bb, br, bt = border
    assert bl + br < im.width and bb + bt < im.height, f"{name}: border 超过图本身"
    im.save(os.path.join(DST, name + ".png"))
    with open(os.path.join(DST, name + ".png.meta"), "w", newline="\n") as f:
        f.write(META.format(guid=guid_for(name), bl=bl, bb=bb, br=br, bt=bt))
    print(f"{name:14s} {im.width}x{im.height}  border L{bl} B{bb} R{br} T{bt}   <- {src}  ({desc})")
    sheet_rows.append((name, im, border))

# 预览：每张图按所有真实按钮尺寸拉一遍，肉眼看端头有没有被拉糊
pad = 12
cell_w = sum(r[0] for r in TEST_RECTS) + pad * (len(TEST_RECTS) + 1)
row_h = max(r[1] for r in TEST_RECTS) + pad
sheet = Image.new("RGBA", (cell_w, row_h * len(sheet_rows)), (60, 66, 78, 255))
for i, (name, im, border) in enumerate(sheet_rows):
    x = pad
    for rect in TEST_RECTS:
        sheet.paste(nine_slice(im, border, rect), (x, i * row_h + pad // 2), nine_slice(im, border, rect))
        x += rect[0] + pad
sheet.save(os.path.join(PREVIEW, "slice_preview.png"))
print("\npreview ->", os.path.join(PREVIEW, "slice_preview.png"))
