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
            if (!TryGetMeshBounds(model, out bounds))
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
            float scale = Mathf.Min(targetX / bounds.size.x, targetZ / bounds.size.z);
            model.transform.localScale *= scale;

            // 缩放绕的是模型自己的轴心，包围盒会跟着挪；必须重测一次再归位，
            // 否则偏移量还是按缩放前的包围盒算的，模型会歪出格子。
            if (!TryGetMeshBounds(model, out bounds))
            {
                return "缩放后测不到包围盒，未归位";
            }

            // 先把包围盒最小角挪到原点，再沿 XZ 各推半个空隙 → 占地矩形内居中；Y 保持贴地
            model.transform.localPosition += new Vector3(
                (targetX - bounds.size.x) * 0.5f,
                0f,
                (targetZ - bounds.size.z) * 0.5f) - bounds.min;

            aligned = true;
            // 打填充率而不是缩放倍数：倍数对美术没有可操作性，填充率能直接看出"这个模型在格子里有多空"
            return $"{cols}×{rows} 格，填充 {bounds.size.x / targetX:P0}×{bounds.size.z / targetZ:P0}" +
                   $"（{bounds.size.x:0.##}×{bounds.size.z:0.##}m / {targetX:0.##}×{targetZ:0.##}m）";
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
        /// 一棵子树里所有网格的世界空间 AABB。
        /// 不用 <see cref="Renderer.bounds"/>：那是 Unity 内部缓存的，编辑器里刚改完 transform
        /// 不保证已经刷新；这里直接把 mesh.bounds 的 8 个角点过一遍 localToWorldMatrix，改完就准。
        ///
        /// 前提：模型是单节点、或子节点只带**轴对齐**旋转（当前 22 个资产都满足）。
        /// 若将来出现带斜角旋转的子节点，「AABB 的角点再取 AABB」会保守放大，
        /// 缩放系数偏小、模型明显缩水；那时要改成逐顶点求（生成期的一次性开销可以接受）。
        /// </summary>
        private static bool TryGetMeshBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Bounds local = mesh.bounds;
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                Vector3 center = local.center;
                Vector3 extents = local.extents;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        center.x + ((i & 1) == 0 ? -extents.x : extents.x),
                        center.y + ((i & 2) == 0 ? -extents.y : extents.y),
                        center.z + ((i & 4) == 0 ? -extents.z : extents.z));
                    Vector3 p = toWorld.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return any;
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

            if (badCount == 0)
            {
                Debug.Log($"[对位校验] 检查了 {checkedCount} 个 Prefab，全部与配表一致。");
            }
            else
            {
                Debug.LogError($"[对位校验] 检查了 {checkedCount} 个 Prefab，其中 {badCount} 个有问题：\n{log}" +
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
