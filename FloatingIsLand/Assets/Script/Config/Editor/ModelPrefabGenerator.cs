using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// 按配表把 Assets/Res/&lt;资产名&gt;/fbx/&lt;资产名&gt;.fbx 批量转成 Resources 下的 Prefab，
    /// 生成结果就是配表 prefabPath 列填的东西（Resources 相对路径，不带扩展名）：
    ///   BuildingVariant → Assets/Resources/Prefab/Building/&lt;variantId&gt;.prefab → "Prefab/Building/&lt;variantId&gt;"
    ///   MapElement      → Assets/Resources/Prefab/Element/&lt;elementId&gt;.prefab  → "Prefab/Element/&lt;elementId&gt;"
    ///   Stage           → Assets/Resources/Prefab/Stage/stage_NN.prefab        → "Prefab/Stage/stage_NN"
    ///
    /// 生成的是引用 FBX 的嵌套 Prefab（不是拍平副本）：美术重导模型后网格/材质自动跟着更新。
    /// 关卡岛屿额外补 MeshCollider——地形描摹刷子要往岛面上打射线。
    ///
    /// **Prefab 结构是两层，这是整条表现链路的地基：**
    /// <code>
    ///   &lt;assetId&gt;        ← 包装根：identity（pos 0 / rot 0 / scale 1）
    ///   └── &lt;FBX 实例&gt;   ← 承载轴向修正、单位换算、按格缩放、轴心偏移
    /// </code>
    /// 为什么必须包一层：这批模型是 Blender 导出的 Z-up FBX，Unity 导入器不改网格数据，
    /// 而是把 Z-up→Y-up 的 -90°X 修正**烤在模型根节点的 Transform 上**。表现层摆放时要写
    /// <c>transform.rotation = Euler(0, yaw, 0)</c>，直接写在模型根上就会把那个修正一并抹掉，
    /// 模型当场躺倒。包一层 identity 的壳，表现层只碰壳、碰不到修正，模型就立得住。
    ///
    /// 顺带把「按 footprint 缩放 + 轴心归位」也放在这里做（早先在 BuildingModelPostprocessor 里）：
    /// 包装根下的坐标系已经是 Y-up，量包围盒才量得对；而且轴心偏移必须落在子节点上，
    /// 导入后处理器够不着（单网格 FBX 的 mesh 就挂在根节点，根节点自己没法相对自己偏移）。
    /// 代价是**改了模型或配表 footprint 之后要重跑一次本菜单**，对齐不再随重导自动更新——
    /// 用 Tools/美术/校验模型对位 可以一键查出哪些 Prefab 和配表失配了。
    /// </summary>
    public static class ModelPrefabGenerator
    {
        private const string ResRoot = "Assets/Res";
        private const string PrefabRoot = "Assets/Resources/Prefab";

        public const string BuildingSubDir = "Building";
        public const string ElementSubDir = "Element";
        public const string StageSubDir = "Stage";

        [MenuItem("Tools/美术/生成白模 Prefab（FBX → Resources/Prefab）", false, 1)]
        public static void GenerateAll()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            var log = new StringBuilder();
            int made = 0;
            int missing = 0;
            int unaligned = 0;

            // 建目录要在 StartAssetEditing 之前：批处理区间里新建的目录不保证立刻对
            // SaveAsPrefabAsset 可见，首次在干净机器上跑会失败
            EnsureFolder($"{PrefabRoot}/{BuildingSubDir}");
            EnsureFolder($"{PrefabRoot}/{ElementSubDir}");
            EnsureFolder($"{PrefabRoot}/{StageSubDir}");

            // 所有临时对象都建在预览场景里，不碰用户正打开的场景。
            // 否则每跑一次菜单当前场景就被标脏，之后切场景/退 Unity 会弹「Save Scene?」模态框——
            // 那个框会把 MCP 连接整条卡死，而且程序点不掉。
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (BuildingVariantRow variant in Tables.BuildingVariant.All)
                {
                    // 资产可能按变体名（residence_01.fbx）也可能按建筑名（farm.fbx）给，两个都试
                    string fbx = ResolveFbx(variant.variantId) ?? ResolveFbx(variant.buildingId);
                    if (Generate(fbx, BuildingSubDir, variant.variantId, variant.footprint, false, preview, log, ref unaligned))
                    {
                        made++;
                    }
                    else
                    {
                        missing++;
                    }
                }

                foreach (MapElementRow element in Tables.MapElement.All)
                {
                    string fbx = ResolveFbx(element.elementId);
                    if (fbx == null)
                    {
                        // 地形类元素（绿地/空岛/浮空区域）与风源本就没有模型，靠地形层着色表现，不算缺失
                        continue;
                    }
                    if (Generate(fbx, ElementSubDir, element.elementId, element.footprint, false, preview, log, ref unaligned))
                    {
                        made++;
                    }
                    else
                    {
                        missing++;
                    }
                }

                foreach (StageRow stage in Tables.Stage.All)
                {
                    string assetId = StageAssetId(stage.stageId);
                    string fbx = ResolveFbx(assetId);
                    // 岛屿没有占地定义，不做按格对齐——入场缩放/居中由 IslandFitter 在运行时和编辑器里统一做
                    if (Generate(fbx, StageSubDir, assetId, null, true, preview, log, ref unaligned))
                    {
                        made++;
                    }
                    else
                    {
                        missing++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // 对齐失败的 Prefab 照样存了盘（缺模型不该拦住整条链路），所以必须让它在结论行里显形，
            // 否则扫一眼"生成 N 个"会以为全绿
            string summary = $"[白模 Prefab] 生成 {made} 个，缺模型 {missing} 个，对齐失败 {unaligned} 个。\n{log}";
            if (unaligned > 0)
            {
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        /// <summary>把多行报告落到 Temp/ 下，返回落盘路径（失败时返回一句说明，不打断调用方）。</summary>
        private static string WriteReport(string fileName, string content)
        {
            string outPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? ".", "Temp/" + fileName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, content);
                return outPath;
            }
            catch (System.Exception e)
            {
                return $"（落盘失败：{e.Message}）";
            }
        }

        /// <summary>关卡岛屿资产名：stageId 1 → stage_01。</summary>
        public static string StageAssetId(int stageId)
        {
            return "stage_" + stageId.ToString("00");
        }

        /// <summary>配表 prefabPath 该填的值（Resources 相对、无扩展名）。</summary>
        public static string ResourcePath(string subDir, string assetId)
        {
            return "Prefab/" + subDir + "/" + assetId;
        }

        private static string ResolveFbx(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
            {
                return null;
            }
            string path = $"{ResRoot}/{assetId}/fbx/{assetId}.fbx";
            return File.Exists(path) ? path : null;
        }

        private static bool Generate(
            string fbxPath, string subDir, string assetId, string[] footprint, bool addCollider,
            Scene workScene, StringBuilder log, ref int unaligned)
        {
            if (fbxPath == null)
            {
                log.AppendLine($"  - 缺模型：{subDir}/{assetId}（Assets/Res/{assetId}/fbx/{assetId}.fbx 不存在，prefabPath 留空走白模占位）");
                return false;
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelAsset == null)
            {
                log.AppendLine($"  - 读不到模型：{fbxPath}");
                return false;
            }

            string dir = $"{PrefabRoot}/{subDir}";
            EnsureFolder(dir);
            string outPath = $"{dir}/{assetId}.prefab";

            // 包装根保持 identity，模型作为子节点挂进去（见类注释：轴向修正必须留在子节点上）
            var root = new GameObject(assetId);
            SceneManager.MoveGameObjectToScene(root, workScene);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, workScene);
                model.transform.SetParent(root.transform, false);

                string alignNote;
                if (footprint == null)
                {
                    alignNote = "无占地定义，未按格对齐";
                }
                else
                {
                    bool aligned;
                    alignNote = AlignToFootprint(model, footprint, assetId, out aligned);
                    if (!aligned)
                    {
                        unaligned++;
                    }
                }

                if (addCollider)
                {
                    AddMeshColliders(root);
                }
                PrefabUtility.SaveAsPrefabAsset(root, outPath);
                log.AppendLine($"  + {outPath}  →  prefabPath \"{ResourcePath(subDir, assetId)}\"（{alignNote}）");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return true;
        }

        /// <summary>
        /// 把模型缩放到 footprint 对应的格数，并把它摆进占地矩形：**XZ 居中、底面 y=0**。
        ///
        /// AI 生成的模型尺寸是随机的，手动逐个对格子太慢；这里以配表为唯一真相自动对齐：
        ///   缩放系数 = min(目标宽 / 包围盒宽, 目标深 / 包围盒深)
        /// 取 min 保证不越格——设计要求可以占不满，但不能超出。
        ///
        /// 为什么必须**居中**而不是「贴占地最小角」：min() 缩放注定有一轴填不满，贴最小角的话
        /// 模型就永远偏向那一侧；而摆放时的旋转补偿（ModelSpawner.PlacementPosition）是按
        /// footprint 的整条 Span 推的，转 180° 后模型会翻到占地矩形的另一侧——同一栋楼滚轮转半圈
        /// 就横跳一整格（miningStation_01 的 2×1 占地实测跳 2m）。居中之后，矩形怎么转模型都还在正中，
        /// 四个朝向下位置一致；这也和白模退化路径（CreateWhiteBox 本来就是居中建盒）统一了口径。
        ///
        /// 调用时 <paramref name="model"/> 已经挂在世界原点上的 identity 包装根下，
        /// 所以「世界坐标」就等于「相对包装根的坐标」，量出来的包围盒可以直接拿来当偏移用。
        /// </summary>
        /// <returns>写进生成日志的一句话说明；<paramref name="aligned"/> 为 false 表示这个模型没对齐成功。</returns>
        private static string AlignToFootprint(GameObject model, string[] footprint, string assetId, out bool aligned)
        {
            aligned = false;

            int cols, rows;
            if (!TryMeasure(footprint, out cols, out rows))
            {
                Debug.LogWarning($"[白模 Prefab] {assetId} 的 footprint 非法，跳过按格对齐。");
                return "footprint 非法，未对齐";
            }

            float cellSize = Tables.GameConfig.cellSize;
            if (cellSize <= 0f)
            {
                Debug.LogError($"[白模 Prefab] GameConfig.cellSize = {cellSize}，无法按格对齐。");
                return "cellSize 非法，未对齐";
            }

            Bounds bounds;
            List<Vector3> vertices = CollectWorldVertices(model);
            if (!TryGetBounds(vertices, out bounds))
            {
                Debug.LogWarning($"[白模 Prefab] {assetId} 没有任何网格，跳过按格对齐。");
                return "无网格，未对齐";
            }

            // 阈值按格长取相对值：Mathf.Epsilon 是 float 的次正规最小值（~1e-45），
            // 拿它当"薄得没法拟合"的门槛等于只挡住了严格的 0，真正的薄片模型照样会漏过去
            float degenerate = cellSize * 1e-4f;
            if (bounds.size.x <= degenerate || bounds.size.z <= degenerate)
            {
                Debug.LogWarning($"[白模 Prefab] {assetId} 的包围盒在 XZ 上退化（{bounds.size.x:0.####}×{bounds.size.z:0.####}m），跳过按格对齐。");
                return "包围盒退化，未对齐";
            }

            float targetX = cols * cellSize;
            float targetZ = rows * cellSize;

            // 先转正、再量包围盒算缩放。顺序反了就是拿斜着的 AABB 去拟合：45° 时 AABB 比模型本身
            // 大 √2 倍，缩放系数跟着小 √2 倍，模型会明显缩水，而且四角照样是空的。
            float yaw = SolveYaw(vertices, targetX, targetZ);

            // 异形掩码还要再定一次向。最小面积外接矩形对「L 形转 180°」完全等价——AABB 一模一样、
            // 缩放系数一模一样，评分分不出高下，于是缺口朝哪边全看凸包边的遍历顺序落在了哪个解上。
            // 表现出来就是「房子和占地格刚好反过来」。满格矩形掩码四个朝向本来就等价，跳过省时间。
            var pose = new ModelPose(model.transform);
            if (!IsFullRectangle(footprint))
            {
                yaw = OrientToMask(model, pose, footprint, cols, rows, cellSize, targetX, targetZ, yaw);
            }

            if (!TryFitAt(model, pose, yaw, targetX, targetZ, out bounds))
            {
                return "拟合失败，未对齐";
            }

            aligned = true;
            // 打填充率而不是缩放倍数：倍数对美术没有可操作性，填充率能直接看出"这个模型在格子里有多空"。
            // 偏航也要打出来：那是几何自动求的，美术需要知道自己的模型被转了多少度才能判断合不合预期。
            return $"{cols}×{rows} 格，填充 {bounds.size.x / targetX:P0}×{bounds.size.z / targetZ:P0}" +
                   $"（{bounds.size.x:0.##}×{bounds.size.z:0.##}m / {targetX:0.##}×{targetZ:0.##}m）" +
                   $"{(Mathf.Abs(yaw) > 0.01f ? $"，转正 {yaw:0.#}°" : "")}";
        }

        /// <summary>模型在对齐前的初始位姿。定向要反复试摆，每次都得从同一个起点重来。</summary>
        private readonly struct ModelPose
        {
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _scale;

            public ModelPose(Transform transform)
            {
                _position = transform.localPosition;
                _rotation = transform.localRotation;
                _scale = transform.localScale;
            }

            public void RestoreTo(Transform transform)
            {
                transform.localPosition = _position;
                transform.localRotation = _rotation;
                transform.localScale = _scale;
            }
        }

        /// <summary>
        /// 从初始位姿出发：施加偏航 → 按格缩放 → 在占地矩形内 XZ 居中、底面贴地。
        /// <paramref name="bounds"/> 返回最终包围盒（尺寸不受最后那步平移影响，可以直接拿去算填充率）。
        /// </summary>
        private static bool TryFitAt(
            GameObject model, ModelPose pose, float yaw, float targetX, float targetZ, out Bounds bounds)
        {
            pose.RestoreTo(model.transform);
            if (Mathf.Abs(yaw) > 0.01f)
            {
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
            }

            if (!TryGetMeshBounds(model, out bounds))
            {
                return false;
            }

            float scale = Mathf.Min(targetX / bounds.size.x, targetZ / bounds.size.z);
            model.transform.localScale *= scale;

            // 缩放绕的是模型自己的轴心，包围盒会跟着挪；必须重测一次再归位，
            // 否则偏移量还是按缩放前的包围盒算的，模型会歪出格子。
            if (!TryGetMeshBounds(model, out bounds))
            {
                return false;
            }

            // 先把包围盒最小角挪到原点，再沿 XZ 各推半个空隙 → 占地矩形内居中；Y 保持贴地
            model.transform.localPosition += new Vector3(
                (targetX - bounds.size.x) * 0.5f,
                0f,
                (targetZ - bounds.size.z) * 0.5f) - bounds.min;
            return true;
        }

        /// <summary>
        /// 异形掩码的定向：四个朝向各摆一次，取模型实际覆盖与配表掩码最贴合的那个。
        ///
        /// 打分用**覆盖率加权**而不是"先卡阈值再数格子"：临界格（覆盖率刚好在阈值附近）会让
        /// 数格子的结果在两个朝向间抖动，而加权分是连续的，差一点点也能稳定分出高下。
        /// 掩码为 '#' 的格子覆盖率算加分、'.' 的算减分——既奖励盖住该盖的，也惩罚压住该空的。
        /// </summary>
        private static float OrientToMask(
            GameObject model, ModelPose pose, string[] footprint,
            int cols, int rows, float cellSize, float targetX, float targetZ, float baseYaw)
        {
            float bestYaw = baseYaw;
            float bestScore = float.MinValue;

            for (int k = 0; k < 4; k++)
            {
                float candidate = Mathf.Repeat(baseYaw + k * 90f, 360f);
                Bounds ignored;
                if (!TryFitAt(model, pose, candidate, targetX, targetZ, out ignored))
                {
                    continue;
                }

                // 包装根是 identity，模型的世界坐标就是相对占地矩形的坐标，可以直接交给探针量
                float[,] coverage = ModelFootprintProbe.MeasureCoverage(model, cols, rows, cellSize);
                if (coverage == null)
                {
                    continue;
                }

                float score = 0f;
                for (int c = 0; c < cols; c++)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        // r 是 dz 方向，掩码第一行在 z 最大侧 → 行号 rows-1-r（与探针同口径）
                        bool solid = footprint[rows - 1 - r][c] == '#';
                        score += solid ? coverage[c, r] : -coverage[c, r];
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestYaw = candidate;
                }
            }

            return bestYaw;
        }

        /// <summary>掩码是不是满格矩形（没有 '.'）。满格时四个朝向等价，不必再定向。</summary>
        private static bool IsFullRectangle(string[] footprint)
        {
            for (int i = 0; i < footprint.Length; i++)
            {
                if (footprint[i].IndexOf('.') >= 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>掩码行列数；行长不一致或全空返回 false。</summary>
        private static bool TryMeasure(string[] footprint, out int cols, out int rows)
        {
            cols = 0;
            rows = 0;
            if (footprint == null || footprint.Length == 0)
            {
                return false;
            }

            cols = footprint[0] == null ? 0 : footprint[0].Length;
            rows = footprint.Length;
            if (cols == 0)
            {
                return false;
            }

            bool hasSolid = false;
            for (int i = 0; i < footprint.Length; i++)
            {
                string line = footprint[i];
                if (line == null || line.Length != cols)
                {
                    return false;
                }
                if (line.IndexOf('#') >= 0)
                {
                    hasSolid = true;
                }
            }
            return hasSolid;
        }

        /// <summary>
        /// 一棵子树里所有网格顶点的世界坐标。
        /// 不用 <see cref="Renderer.bounds"/>：那是 Unity 内部缓存的，编辑器里刚改完 transform
        /// 不保证已经刷新；这里直接把顶点过一遍 localToWorldMatrix，改完就准。
        ///
        /// 走逐顶点而不是「mesh.bounds 的 8 个角点」：<see cref="SolveYaw"/> 会给模型施加**斜角**偏航，
        /// 而「AABB 的角点再取 AABB」在斜角下会保守放大（45° 时放大 √2 倍），
        /// 缩放系数跟着偏小、模型明显缩水。22 个低模在生成期跑一遍的开销可以忽略。
        /// </summary>
        private static List<Vector3> CollectWorldVertices(GameObject root)
        {
            var result = new List<Vector3>();
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices;
                try
                {
                    vertices = mesh.vertices;
                }
                catch (System.Exception e)
                {
                    // 与占格探针同口径：读不到就跳过并留一条可定位的警告，不能静默当成"没有网格"
                    Debug.LogWarning($"[白模 Prefab] 读不到 {mesh.name} 的顶点：{e.Message}");
                    continue;
                }
                if (vertices == null || vertices.Length == 0)
                {
                    continue;
                }

                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    result.Add(toWorld.MultiplyPoint3x4(vertices[i]));
                }
            }

            return result;
        }

        /// <summary>
        /// 求一个绕 Y 的偏航角，把模型的地基转到与网格轴平行。
        ///
        /// 为什么需要它：这批资产是从**等距 45° 概念图**生成的，模型的地基跟着概念图斜了 45°，
        /// 而占地格是轴对齐的。斜着的方形地基硬塞进轴对齐的占地矩形，结果是四角空、边缘溢出观感——
        /// 玩家看到的就是「建筑和格子差 45°」。实测证据是占格探针打出来的标准菱形覆盖率
        /// （四角 ~12%、边中 ~83%、中心 100%，见 workshop_01）。
        ///
        /// 解法用最小面积外接矩形：**最优矩形必有一条边与凸包的某条边共线**（经典结论），
        /// 所以只要逐条凸包边试过去就能求到精确解，不必按角度暴力扫。
        /// 评分直接取 <c>min(目标宽/实测宽, 目标深/实测深)</c>——也就是 <see cref="AlignToFootprint"/>
        /// 真正会用的那个缩放系数，于是「转正」和「长边对长边」一步到位：
        /// 2×1 占地的模型不会被转 90° 去横躺，因为那个方向的评分更低。
        ///
        /// 只按几何求解、不给每个资产手配角度：手配的表会和美术重出的资产悄悄失同步，
        /// 而这里每次生成都重算一遍，模型换了自动跟上。
        /// </summary>
        /// <param name="vertices">模型的世界空间顶点（包装根是 identity，等同于相对包装根）。</param>
        /// <returns>该施加的偏航角（度）；求不出时返回 0。</returns>
        private static float SolveYaw(List<Vector3> vertices, float targetX, float targetZ)
        {
            List<Vector2> hull = ConvexHullXZ(vertices);
            if (hull.Count < 3)
            {
                // 点太少（退化成线或点）时旋转没有意义，保持原样交给后面的退化分支报
                return 0f;
            }

            float bestYaw = 0f;
            float bestScore = ScoreYaw(hull, 0f, targetX, targetZ);

            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 edge = hull[(i + 1) % hull.Count] - hull[i];
                if (edge.sqrMagnitude <= Mathf.Epsilon)
                {
                    continue;
                }

                // 把这条边转到与 X 轴平行需要的偏航。Unity 的 Euler(0,yaw,0) 作用在 (x,z) 上
                // 相当于把方位角减去 yaw，所以直接取这条边自己的方位角。
                float yaw = Mathf.Atan2(edge.y, edge.x) * Mathf.Rad2Deg;
                // 同时试 +90°：凸包不保证两个正交方向的边都存在，而长宽该怎么对应由评分决定
                for (int k = 0; k < 2; k++)
                {
                    float candidate = Mathf.Repeat(yaw + k * 90f, 360f);
                    float score = ScoreYaw(hull, candidate, targetX, targetZ);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestYaw = candidate;
                    }
                }
            }

            // 本来就轴对齐的资产不要为了千分之几的评分去转一个碎角度——那只会让美术对不上账。
            // 阈值取 0.5%：45° 的错位会带来 √2 ≈ 41% 的差距，远在阈值之上，不会被这条挡掉。
            float upright = ScoreYaw(hull, 0f, targetX, targetZ);
            if (upright >= bestScore * 0.995f)
            {
                return 0f;
            }
            return bestYaw;
        }

        /// <summary>某个偏航下模型能吃到的缩放系数（越大越贴合占地）。退化方向返回 0。</summary>
        private static float ScoreYaw(List<Vector2> hull, float yaw, float targetX, float targetZ)
        {
            float rad = yaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < hull.Count; i++)
            {
                // 与 Quaternion.Euler(0, yaw, 0) 同一套旋转：+X 转向 -Z
                float x = hull[i].x * cos + hull[i].y * sin;
                float z = -hull[i].x * sin + hull[i].y * cos;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }

            float width = maxX - minX;
            float depth = maxZ - minZ;
            if (width <= Mathf.Epsilon || depth <= Mathf.Epsilon)
            {
                return 0f;
            }
            return Mathf.Min(targetX / width, targetZ / depth);
        }

        /// <summary>顶点在 XZ 平面上的凸包（Andrew monotone chain，逆时针）。</summary>
        private static List<Vector2> ConvexHullXZ(List<Vector3> vertices)
        {
            var points = new List<Vector2>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                points.Add(new Vector2(vertices[i].x, vertices[i].z));
            }

            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            var hull = new List<Vector2>(points.Count + 1);
            // 下链 → 上链；每条链里把造成非左转的点弹掉
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 p = pass == 0 ? points[i] : points[points.Count - 1 - i];
                    while (hull.Count >= start + 2
                           && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                    {
                        hull.RemoveAt(hull.Count - 1);
                    }
                    hull.Add(p);
                }
                // 每条链的终点是下一条链的起点，去掉重复
                hull.RemoveAt(hull.Count - 1);
            }

            return hull;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        /// <summary>一棵子树里所有网格顶点的世界空间 AABB。</summary>
        private static bool TryGetMeshBounds(GameObject root, out Bounds bounds)
        {
            return TryGetBounds(CollectWorldVertices(root), out bounds);
        }

        private static bool TryGetBounds(List<Vector3> vertices, out Bounds bounds)
        {
            bounds = new Bounds();
            if (vertices == null || vertices.Count == 0)
            {
                return false;
            }

            bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }
            return true;
        }

        private static void AddMeshColliders(GameObject root)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshCollider>() != null)
                {
                    continue;
                }
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
        }

        private static void EnsureFolder(string assetDir)
        {
            if (AssetDatabase.IsValidFolder(assetDir))
            {
                return;
            }

            string[] parts = assetDir.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        /// <summary>
        /// 打印各关卡岛屿模型的世界包围盒——决定网格铺多大、岛要不要挪。
        /// </summary>
        [MenuItem("Tools/美术/打印岛屿模型尺寸", false, 2)]
        public static void LogStageBounds()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            Debug.Log("[岛屿尺寸] cellSize=" + Tables.GameConfig.cellSize);
            // 同 GenerateAll：临时实例一律建在预览场景里，别把用户的场景标脏
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (StageRow stage in Tables.Stage.All)
                {
                    string assetId = StageAssetId(stage.stageId);
                    string fbx = ResolveFbx(assetId);
                    if (fbx == null)
                    {
                        Debug.Log($"[岛屿尺寸] {assetId}: 无模型");
                        continue;
                    }

                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                    try
                    {
                        Bounds bounds;
                        if (!TryGetWorldBounds(instance, out bounds))
                        {
                            Debug.Log($"[岛屿尺寸] {assetId}: 无网格");
                            continue;
                        }
                        float cellSize = Tables.GameConfig.cellSize;
                        Debug.Log(
                            $"[岛屿尺寸] {assetId}: 尺寸 {bounds.size.x:0.##} × {bounds.size.y:0.##} × {bounds.size.z:0.##} m" +
                            $"，中心 ({bounds.center.x:0.##}, {bounds.center.y:0.##}, {bounds.center.z:0.##})" +
                            $"，顶面 y={bounds.max.y:0.##}" +
                            $"  → 约 {Mathf.CeilToInt(bounds.size.x / cellSize)} × {Mathf.CeilToInt(bounds.size.z / cellSize)} 格");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        /// <summary>
        /// 校验已生成的 Prefab 是否还和配表对得上。
        ///
        /// 对齐结果是烤进 Prefab 的，不随 FBX 重导自动更新——改了模型或改了配表 footprint
        /// 却忘了重跑生成菜单，表现上就是「模型比格子大一圈 / 小一圈」或「偏出占地」，
        /// 而且不会有任何报错。这个菜单把那种失配一次性查出来。
        /// </summary>
        [MenuItem("Tools/美术/校验模型对位", false, 3)]
        public static void ValidateAlignment()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            var log = new StringBuilder();
            int checkedCount = 0;
            int badCount = 0;

            foreach (BuildingVariantRow variant in Tables.BuildingVariant.All)
            {
                if (Validate(BuildingSubDir, variant.variantId, variant.prefabPath, variant.footprint, false, log, ref checkedCount))
                {
                    badCount++;
                }
            }

            foreach (MapElementRow element in Tables.MapElement.All)
            {
                if (Validate(ElementSubDir, element.elementId, element.prefabPath, element.footprint, false, log, ref checkedCount))
                {
                    badCount++;
                }
            }

            foreach (StageRow stage in Tables.Stage.All)
            {
                // 岛屿不按格对齐，但必须是包装根结构 + 带 MeshCollider（描摹刷子要打射线）
                string assetId = StageAssetId(stage.stageId);
                if (Validate(StageSubDir, assetId, stage.prefabPath, null, true, log, ref checkedCount))
                {
                    badCount++;
                }
            }

            // 与占格探针同口径落一份到文件：Console 只显示日志首行，逐条明细在里面根本看不全，
            // 而"哪几个不对"恰恰全在后面那些行里
            string outPath = WriteReport("alignment_validate.txt", log.ToString());

            if (badCount == 0)
            {
                Debug.Log($"[对位校验] 检查了 {checkedCount} 个 Prefab，全部与配表一致。明细：{outPath}");
            }
            else
            {
                Debug.LogError($"[对位校验] 检查了 {checkedCount} 个 Prefab，其中 {badCount} 个有问题。明细：{outPath}\n{log}" +
                               "\n多数情况跑一次 Tools/美术/生成白模 Prefab 重新生成即可；配表没填的要回配表补。");
            }
        }

        /// <summary>
        /// 校验单个 Prefab；返回 true 表示发现问题。
        ///
        /// 用 <see cref="PrefabUtility.LoadPrefabContents"/> 而不是往当前场景里 InstantiatePrefab：
        /// 后者会把用户正打开的场景标脏，之后切场景/退 Unity 会弹「Save Scene?」模态框，
        /// 而模态框会把 MCP 连接整条卡死。校验是只读操作，不该有任何副作用。
        /// </summary>
        private static bool Validate(
            string subDir, string assetId, string tablePrefabPath, string[] footprint, bool needCollider,
            StringBuilder log, ref int checkedCount)
        {
            string path = $"{PrefabRoot}/{subDir}/{assetId}.prefab";
            bool onDisk = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            string expectedPath = ResourcePath(subDir, assetId);
            bool tableFilled = !string.IsNullOrEmpty(tablePrefabPath);

            if (!onDisk)
            {
                if (tableFilled)
                {
                    // 配表指着一个不存在的 Prefab：运行时会静默退化成白模方块，必须报出来
                    checkedCount++;
                    log.AppendLine($"  · {subDir}/{assetId}：配表 prefabPath 填了 \"{tablePrefabPath}\"，但 {path} 不存在。");
                    return true;
                }
                // 配表没填、磁盘也没有 = 美术资产还没到位，走白模占位，不算错
                return false;
            }

            if (!tableFilled)
            {
                // 反向失配：Prefab 生成了但配表那一列没回填，游戏里永远走白模，而且哪里都不会报
                checkedCount++;
                log.AppendLine($"  · {subDir}/{assetId}：Prefab 已生成，但配表 prefabPath 为空（应填 \"{expectedPath}\"）。");
                return true;
            }
            if (!string.Equals(tablePrefabPath, expectedPath, System.StringComparison.Ordinal))
            {
                checkedCount++;
                log.AppendLine($"  · {subDir}/{assetId}：配表 prefabPath 是 \"{tablePrefabPath}\"，应为 \"{expectedPath}\"。");
                return true;
            }

            checkedCount++;
            GameObject instance = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform root = instance.transform;

                // 1) 包装根必须是 identity，否则表现层赋位姿会把它上面的东西一起冲掉
                if (root.localPosition.sqrMagnitude > 1e-6f
                    || Quaternion.Angle(root.localRotation, Quaternion.identity) > 0.01f
                    || (root.localScale - Vector3.one).sqrMagnitude > 1e-6f)
                {
                    log.AppendLine($"  · {subDir}/{assetId}：根节点不是 identity" +
                                   $"（pos {root.localPosition}, rot {root.localRotation.eulerAngles}, scale {root.localScale}）" +
                                   "——多半是旧结构的 Prefab，没重新生成过。");
                    return true;
                }

                if (needCollider && instance.GetComponentInChildren<MeshCollider>(true) == null)
                {
                    log.AppendLine($"  · {subDir}/{assetId}：没有 MeshCollider，地形描摹会一格都命中不了。");
                    return true;
                }

                if (footprint == null)
                {
                    return false;
                }

                int cols, rows;
                if (!TryMeasure(footprint, out cols, out rows))
                {
                    log.AppendLine($"  · {subDir}/{assetId}：配表 footprint 非法。");
                    return true;
                }

                Bounds bounds;
                if (!TryGetMeshBounds(instance, out bounds))
                {
                    log.AppendLine($"  · {subDir}/{assetId}：没有任何网格。");
                    return true;
                }

                float cellSize = Tables.GameConfig.cellSize;
                if (cellSize <= 0f)
                {
                    log.AppendLine($"  · {subDir}/{assetId}：GameConfig.cellSize = {cellSize}，无法校验。");
                    return true;
                }

                float targetX = cols * cellSize;
                float targetZ = rows * cellSize;
                // 容差按格长取，不用绝对值：cellSize 变了阈值也跟着变
                float tolerance = cellSize * 0.01f;

                // 2) 模型必须在占地矩形内 XZ 居中、底面贴地。
                //    居中是关键：只要偏心，转 180° 就会翻到矩形另一侧、整体跳一格（见 AlignToFootprint 注释）
                if (Mathf.Abs(bounds.center.x - targetX * 0.5f) > tolerance
                    || Mathf.Abs(bounds.center.z - targetZ * 0.5f) > tolerance)
                {
                    log.AppendLine($"  · {subDir}/{assetId}：模型没在占地矩形内居中，" +
                                   $"实测中心 ({bounds.center.x:0.###}, {bounds.center.z:0.###})，" +
                                   $"应为 ({targetX * 0.5f:0.###}, {targetZ * 0.5f:0.###})——转 180° 会跳一格。");
                    return true;
                }
                if (Mathf.Abs(bounds.min.y) > tolerance)
                {
                    log.AppendLine($"  · {subDir}/{assetId}：底面不在 y=0（实测 {bounds.min.y:0.###}），模型会浮空或陷地。");
                    return true;
                }

                // 3) 不能越格，且按 min 取的缩放保证至少有一维正好贴满
                float fillX = bounds.size.x / targetX;
                float fillZ = bounds.size.z / targetZ;
                if (fillX > 1f + 1e-3f || fillZ > 1f + 1e-3f)
                {
                    log.AppendLine($"  · {subDir}/{assetId}：模型越出占地，实测 {bounds.size.x:0.##}×{bounds.size.z:0.##}m" +
                                   $" > 配表 {cols}×{rows} 格 = {targetX:0.##}×{targetZ:0.##}m。");
                    return true;
                }
                if (Mathf.Max(fillX, fillZ) < 1f - 1e-3f)
                {
                    log.AppendLine($"  · {subDir}/{assetId}：模型没缩放到位，实测 {bounds.size.x:0.##}×{bounds.size.z:0.##}m，" +
                                   $"配表 {cols}×{rows} 格 = {targetX:0.##}×{targetZ:0.##}m（长边应当贴满）。");
                    return true;
                }

                return false;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return any;
        }
    }
}
