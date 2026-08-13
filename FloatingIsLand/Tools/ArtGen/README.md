# ArtGen — AI 美术资产流水线

调莉刻（LiClick）平台，从配表里的建筑/地图元素清单，一路产出 **效果图 → 三视图 → low poly FBX**，
按资产分目录落到 `Assets/Res/<资产名>/`。

```
manifest.tsv （资产清单：id / 画幅 / 提示词）
   │
   ├─ run_stage_a.sh  →  Assets/Res/<id>/picture/concept.jpg   效果图
   ├─ run_stage_b.sh  →  Assets/Res/<id>/picture/{front,side,top}.jpg   三视图（正/侧/俯）
   ├─ run_stage_c.sh  →  Assets/Res/<id>/fbx/<id>.fbx          low poly 模型
   └─ fbx_shrink.py   →  压缩 FBX 内嵌贴图（4K → 512），几何不动
```

## 跑法

```bash
cd <Unity 工程根>
bash Tools/ArtGen/run_stage_a.sh     # 效果图
bash Tools/ArtGen/run_stage_b.sh     # 三视图（需先有 concept）
bash Tools/ArtGen/run_stage_c.sh     # 3D 模型（需先有三视图）
python3 Tools/ArtGen/fbx_shrink.py <in.fbx> <out.fbx> 512 85
python3 Tools/ArtGen/fbx_probe.py .  # 质检：读 FBX 二进制报顶点/面数
```

脚本可重复执行：已产出的资产自动跳过，只补缺的。任务状态记在 `ArtGen/state/<id>.*`
（`.task` = 任务 id，`.url` = 结果链接，`.fail` = 失败详情），删掉对应状态文件即可重跑单个资产。

## 三个必须知道的坑（都已在脚本里规避）

1. **上传超限**：网关 `upload_asset` 对大文件返回 `HTTP 413`，2K PNG（约 3MB）必挂，且是**静默失败**——
   后续生成任务根本不会提交。所以参考图一律先压成 1024/1280px JPEG 存 `ArtGen/{refs,views}/` 再上传。
2. **缩略图冒充模型**：3D 任务在 `Processing` 阶段的响应里就带 `thumbnail` 的 jpg 链接，
   "取第一个 https" 会把 71KB 的 JPEG 存成 `.fbx`。只认 `.fbx/.glb/.obj/.stl/.usdz` 扩展名的 URL。
3. **平台偶发生成失败**：出现 "未能生成图像，请调整 prompt" 时不是超时，重试同样的提示词大概率还是失败，
   要把提示词改简单（去掉 "orthographic/no perspective distortion" 这类堆叠约束）再提交。

## 重出已有资产（2026-08-09 踩坑记录）

**"跳过已产出的"是靠缓存实现的，而缓存有三层，只清一层会静默沿用旧内容。** 重出一个资产必须四样一起清：

```bash
id=residence_02
rm -f Assets/Res/$id/fbx/$id.fbx          # 不删 .fbx.meta，保住 GUID
rm -f Assets/Res/$id/picture/{front,side,top}.png
rm -f ArtGen/state/$id.*                  # 任务 id / 结果链接 / 失败标记
rm -f ArtGen/refs/$id.jpg ArtGen/views/$id.*.jpg   # ← 最容易漏的两个
```

`ArtGen/refs/` 与 `ArtGen/views/` 是**必需输入，但没有任何脚本会生成它们**——三个 stage 都只读不写。
漏清的后果都是静默的、而且是部分的：

- 漏清 `refs/` → `run_stage_b.sh` 上传的是旧概念的压缩图（`concept.png` 只是它的 fallback），
  照着旧图重出一整套三视图，**零报错**。现象是"concept 明明换了，三视图还是老样子"。
- 漏清 `views/` → `run_stage_c.sh` 的 `uploadview` 回退去传 2K PNG，触发上面第 1 条的 413，
  日志里只有一行 `[FAIL-upload-views]`。小于 ~1.5MB 的视图会侥幸通过，于是**只挂一部分资产**，
  看起来像偶发。

清完要按新概念重建压缩图（1280px / quality 88 / 约 100KB）：

```python
from PIL import Image
im = Image.open(src).convert("RGB"); im.thumbnail((1280, 1280), Image.LANCZOS)
im.save(dst, "JPEG", quality=88, optimize=True)
```

## 异形占地：别让原 concept 当参考图

L 形 / 凹形占地光靠提示词写清格子数是不可靠的。实测四轮（nano_banana_pro）：

| 输入 | 结果 |
|---|---|
| 只写文字描述 L 形 | 3 块 V 形（照抄参考图） |
| + 俯视平面引导图 | 4 块，但排成 2×2 |
| + 等轴测地基引导图 **+ 原 concept** | 又回到 3 块 V 形 |
| **只给等轴测地基引导图，去掉 concept** | ✅ 正确 |

两条结论：

1. **原 `concept.jpg` 当风格参考是有害的。** 只要它在 `reference_images` 里，构图就被拽回旧样，
   文字里写多少遍 "exactly four tiles" 都没用。风格靠 `run_stage_a.sh` 里的 `STYLE` 常量就够。
2. **把"布局生成"降级成"编辑"。** 先用 `make_plate_guide.py` 按配表把地基渲出来（形状来自配表，不可能错），
   再让模型只做"往这块地基上加房子"：

```bash
python3 Tools/ArtGen/make_plate_guide.py residence_02   # → ArtGen/guides/residence_02.png
# 上传这张，提示词强调：这张图就是最终地基，只许往上加建筑，不许改轮廓/增减格子/转相机
```

**遗留问题**：这条路子能保证格数和长宽比，但**保证不了朝向**——AI 出来的模型可能相对掩码转了
90/180/270°。`ModelPrefabGenerator.SolveYaw` 会自动纠偏，但它的评分是"包围盒能吃到的缩放系数"，
0° 与 180° 的 AABB 完全相同、分不出来。判据得换成逐格掩码吻合度（见 `ModelFootprintProbe`）。

## 导入 Unity 后必跑一步：提取材质与贴图

**FBX 里的贴图是内嵌的，Unity 不会自动展开** —— 只会建一个空材质（Albedo 留白），模型看起来就是白模。
这不是模型没贴图，rodin 每个模型都自带 `texture_diffuse / _normal / _metallic / _roughness` 四张图。

跑菜单 **Tools → 美术 → 提取模型材质与贴图（FBX → mat/）**（
[ModelMaterialExtractor.cs](../../Assets/Script/Config/Editor/ModelMaterialExtractor.cs)），它会：

1. 把内嵌贴图提取到 `Assets/Res/<资产名>/mat/`，法线图自动标成 Normal map
2. 把内嵌材质提取成 `mat/<资产名>.mat`，并写进 FBX 的 `externalObjects`（以后重新导入模型不会覆盖）
3. 按文件名把贴图挂到材质槽：diffuse→`_MainTex`、normal→`_BumpMap`、metallic→`_MetallicGlossMap`，
   并把 `_Color` 复位成白色（否则会和贴图相乘偏色）

**新生成一批模型后要重跑一次**。已有的 Prefab 不带材质覆盖，会自动跟随 FBX 更新，不用重新生成。

### 这一步的三个坑（都踩过）

1. **提取循环不能包 `AssetDatabase.StartAssetEditing()`。** 那会把导入延迟到 `StopAssetEditing` 之后统一做，而
   `ExtractAsset` 依赖「写完 remap 立刻重新导入 FBX」才能把材质绑到渲染器上。包进批处理的结果是：
   `.meta` 的 `externalObjects` 和 `.mat` 的贴图引用**全都是对的**，运行时渲染器上却挂着 `Default-Material`
   —— 磁盘配置正确、画面就是不对，极难定位。
2. **校验要看渲染器实际绑定，不能只看 `.meta`。** 跑菜单 **Tools/美术/重新导入模型并校验材质绑定**，
   它强制重导后逐个检查 `MeshRenderer.sharedMaterials` 里有没有 `Default-Material`（Unity 找不到材质时的替身）。
3. **纯品红不是材质丢失。** 材质真丢渲染成**灰白**。品红查两处：装了 URP 包但没启用管线
   （`Shader.Find("URP/Lit")` 找得到、Built-in 下所有 SubShader 被跳过），或 `TerrainOverlayRenderer`
   的 `fallbackColor` 配色表缺 elementId、半透明盖在模型上。

> 通用版流水线（含全部脚本与两侧陷阱）已抽成 `ai-3d-asset-pipeline` skill，落地到别的工程直接用它。

## 贴图压缩（fbx_shrink.py）

rodin 产出的 FBX 内嵌 4K PBR 贴图，单个 20~30MB。`fbx_shrink.py` 做完整的
FBX 二进制 parse → 改贴图 → 重新序列化（FBX 记录头里的 EndOffset 是**文件绝对偏移**，
改任何内容都必须整棵树重算，不能就地打补丁）。

- 保留贴图原始编码格式：FBX 另存 `Filename` 带扩展名，内容与扩展名不符时 Unity 提取内嵌贴图会失败
- 写完自动回读比对顶点数/面索引数，不一致直接丢弃产物报错
- 实测 22 个模型：530MB → 30MB，几何 100% 不变

## 与配表的关系

`manifest.tsv` 的资产 id 对齐 `Tables/FloatingIsland.xlsx`：建筑 id → `Building.buildingId`，
变体 id（如 `residence_02`）→ `BuildingVariant.variantId`，地图元素 → `MapElement.elementId`。
**占地格数以配表 `footprint` 为准**，效果图上的底板格子只是给 AI 的比例参考，模型导入后需按格宽缩放对齐。

## tripo-v3.1 路线（2026-08-13 起）

概念图已有人工三视图时跳过 stage a/b，直接：切图 → `tripo_driver.py` 生成 → `convert_all_glb.sh` 转 FBX → `swap_in_fbx.sh` 换入。

- **切图**：概念图多为「正/侧/俯」横排白底，面板间常有几像素级粘连，纯列扫描不可靠——
  由子代理逐图目检定切点（spec 落 `crop_one.py` 的 JSON），俯视图只存档。
- **tripo-v3.1 硬限制**：只收 front/left/back/right（传 top 报 400 code 10030）；产物只有 GLB
  （无 geometry_file_format 参数）；不限面数时默认 ~50 万三角面/资产，贴图 jpg 仅 ~0.4MB。
- **429 限流**：一次性提交 31 个任务只放行 ~10 个并发，其余全部 "exceeded the limit"。
  `tripo_driver.py` 以 ≤3 并发滚动提交、429 自动清任务重排队（冷却 120s，每资产最多 6 次）。
  注意网关把状态 JSON 转义进 content[0].text，bash grep 原始引号匹配不到 `"status": "Failed"`。
- **GLB→FBX**：便携 Blender（D:/tools/blender-4.5.3-windows-x64）跑 `convert_glb2fbx.py`，
  贴图数据块按语义改名（Color→<id>_texture_diffuse、NormalGL→<id>_texture_normal，ORM 弃用——
  通道布局 R=AO/G=Rough/B=Metal，直接当 _MetallicGlossMap 挂会错）。
- **换入后清旧贴图**：旧 rodin 的 `texture_diffuse.png` 与新 `<id>_texture_diffuse.jpg` 会同时
  命中提取器的语义匹配，重生成的资产必须先删旧贴图再重跑提取。
- **长菜单不要走 MCP execute_menu_item**：提取/生成把主线程阻塞几分钟，桥心跳超时被 hub 判掉线。
  用 `Assets/Editor/MenuQueueRunner.cs`：菜单路径逐行写 `Temp/menu_queue.txt`，
  结果看 `Temp/menu_queue_result.txt`（每行 ok/fail+耗时，收尾 `=== done ===`）。
- **民居拼装**：`ResidenceAssembler.cs` 把 `Assets/Res/resHouse_NN` 的 1×1 房子按 BuildingVariant
  footprint 拼 田/L/凹；收尾做「整块 min-fit 缩放 + 重居中」，否则长边差 0.2% 过不了对位校验。
- **装饰品**：`DecoPrefabGenerator.cs` 扫 `Assets/Res/deco_*` → `Prefab/Deco/`，按前缀配目标尺寸。
