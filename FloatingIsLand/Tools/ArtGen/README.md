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
