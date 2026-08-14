using System.Collections.Generic;
using System.IO;
using System.Text;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FloatingIsLand.ViewEGB.EditorTools
{
    /// <summary>
    /// 把地图格子的地形语义烘进岛屿模型自己的漫反射贴图：
    /// greenField（可建农田）格子铺草方格块、island 格子铺泥土块，悬崖侧面保留原贴图。
    ///
    /// 为什么是烘贴图而不是叠面片：TerrainOverlayRenderer 那层是建造模式的 UI 语义色，
    /// 常态下不显示；用户要的是岛屿模型本体在任何时候都能看出哪里能种田。
    ///
    /// 落盘策略（调研结论）：
    /// - 读网格：Prefab 实际引用的是压平网格 Resources/Prefab/Stage/Meshes/stage_0N_flat.asset
    ///   （UV 与原 FBX 完全一致、isReadable=1），不是 FBX 原始网格。
    /// - 写贴图：另存 mat/texture_baked.png，源图 texture_diffuse.png 永远不动 —— 烘焙幂等，
    ///   每次都从纯净源图起烘；重跑材质提取也不会把烘焙结果冲掉。
    /// - 指材质：mat/&lt;id&gt;.mat 与 fbx/Materials/texture_diffuse.mat 两份都指过去。工程里
    ///   externalObjects remap 与 legacy External 模式的胜负关系至今有歧义（两处注释互相矛盾、
    ///   两张贴图逐字节相同导致视觉无法区分），双指成本为零且对两种导入行为都免疫。
    /// - 悬崖判据：三角形世界面法线 y &lt; 0.5 不烘 —— 与 IslandSurfaceFlattener 判定顶面的
    ///   阈值同口径，压平工具没动过的顶点这里也不会动它的贴图。
    /// </summary>
    public static class IslandGroundTextureBaker
    {
        private const string TileDir = "Assets/ArtRes/地表贴图块";
        private const int BakeSize = 1024;   // 源图 512：格子在贴图上只有 ~13px，翻倍到 ~26px 草笔触才读得出来
        private const int TileSampleSize = 64; // 贴图块降采样后再采样，避免 256px 细节欠采样成噪点
        private const float TopNormalY = 0.5f; // 顶面阈值，与 IslandSurfaceFlattener 同口径

        [MenuItem("Tools/美术/烘焙岛屿地表贴图（草方格 → texture_baked）", false, 6)]
        public static void BakeAll()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            Color32[][] grass = LoadTiles("grass", 4);
            Color32[][] dirt = LoadTiles("dirt", 4);
            if (grass == null || dirt == null)
            {
                Debug.LogError($"[烘焙] 读不到 {TileDir} 下的草/泥贴图块，中止。");
                return;
            }

            var log = new StringBuilder();
            int done = 0;
            foreach (var stage in Tables.Stage.All)
            {
                if (stage.islandCellSpan <= 0 || string.IsNullOrEmpty(stage.prefabPath))
                {
                    continue;
                }
                if (BakeStage(stage.stageId, stage.prefabPath, stage.islandCellSpan, grass, dirt, log))
                {
                    done++;
                }
            }

            string reportPath = Path.Combine("Temp", "island_bake_report.txt");
            File.WriteAllText(reportPath, log.ToString());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[烘焙] 岛屿地表贴图完成 {done} 关，明细见 {reportPath}");
        }

        private static bool BakeStage(int stageId, string prefabPath, int cellSpan,
            Color32[][] grass, Color32[][] dirt, StringBuilder log)
        {
            string assetId = prefabPath.Substring(prefabPath.LastIndexOf('/') + 1); // stage_01
            string mapPath = $"Assets/Resources/Maps/stage_{stageId}.json";         // 地图文件不补零
            string srcPath = $"Assets/Res/{assetId}/mat/texture_diffuse.png";
            string outPath = $"Assets/Res/{assetId}/mat/texture_baked.png";
            string prefabAssetPath = $"Assets/Resources/{prefabPath}.prefab";

            if (!File.Exists(mapPath) || !File.Exists(srcPath))
            {
                log.AppendLine($"[skip] {assetId}: 缺 {(File.Exists(mapPath) ? srcPath : mapPath)}");
                return false;
            }

            MapSnapshot map = MapJson.Load($"stage_{stageId}", File.ReadAllText(mapPath));
            float cellSize = Tables.GameConfig.cellSize;
            // CenterOrigin 网格挂在世界原点：格 (0,0) 的 min 角 = (-W/2·cell, ·, -L/2·cell)
            float originX = -map.Width * cellSize * 0.5f;
            float originZ = -map.Length * cellSize * 0.5f;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            if (prefab == null)
            {
                log.AppendLine($"[skip] {assetId}: 找不到 {prefabAssetPath}");
                return false;
            }

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                MeshFilter filter = root.GetComponentInChildren<MeshFilter>();
                Renderer renderer = root.GetComponentInChildren<Renderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null)
                {
                    log.AppendLine($"[skip] {assetId}: prefab 没有网格");
                    return false;
                }

                // 复刻 IslandFitter 的 XZ 对位（长边缩到 span×cell、中心对到地图中心=世界原点）。
                // Y 对位刻意跳过：像素→格子只用 XZ，layer 恒 0（三张图都是单层）。
                Bounds b = renderer.bounds;
                float longest = Mathf.Max(b.size.x, b.size.z);
                if (longest < 0.001f)
                {
                    log.AppendLine($"[skip] {assetId}: 包围盒异常");
                    return false;
                }
                root.transform.localScale *= cellSpan * cellSize / longest;
                b = renderer.bounds;
                root.transform.position -= new Vector3(b.center.x, 0f, b.center.z);

                Mesh mesh = filter.sharedMesh;
                Matrix4x4 l2w = filter.transform.localToWorldMatrix;
                Vector3[] verts = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                int[] tris = mesh.triangles;

                // 源图 512 → 双线性放大到 1024 当底；悬崖与未烘区域保持这份原样
                Color32[] basePixels = LoadPngPixels(srcPath, out int srcW, out int srcH);
                if (basePixels == null)
                {
                    log.AppendLine($"[skip] {assetId}: 源贴图读取失败");
                    return false;
                }
                Color32[] outPixels = ResizeBilinear(basePixels, srcW, srcH, BakeSize, BakeSize);

                long painted = 0, grassPx = 0, dirtPx = 0;
                var cellsHit = new HashSet<long>();

                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 w0 = l2w.MultiplyPoint3x4(verts[tris[t]]);
                    Vector3 w1 = l2w.MultiplyPoint3x4(verts[tris[t + 1]]);
                    Vector3 w2 = l2w.MultiplyPoint3x4(verts[tris[t + 2]]);
                    Vector3 faceN = Vector3.Cross(w1 - w0, w2 - w0);
                    if (faceN.sqrMagnitude < 1e-12f || faceN.normalized.y < TopNormalY)
                    {
                        continue; // 悬崖/侧面/底面：保留原贴图
                    }

                    Vector2 uv0 = uvs[tris[t]] * BakeSize;
                    Vector2 uv1 = uvs[tris[t + 1]] * BakeSize;
                    Vector2 uv2 = uvs[tris[t + 2]] * BakeSize;

                    float minX = Mathf.Min(uv0.x, Mathf.Min(uv1.x, uv2.x));
                    float maxX = Mathf.Max(uv0.x, Mathf.Max(uv1.x, uv2.x));
                    float minY = Mathf.Min(uv0.y, Mathf.Min(uv1.y, uv2.y));
                    float maxY = Mathf.Max(uv0.y, Mathf.Max(uv1.y, uv2.y));

                    float denom = (uv1.y - uv2.y) * (uv0.x - uv2.x) + (uv2.x - uv1.x) * (uv0.y - uv2.y);
                    if (Mathf.Abs(denom) < 1e-6f)
                    {
                        continue; // UV 退化三角形
                    }

                    int px0 = Mathf.Max(0, Mathf.FloorToInt(minX));
                    int px1 = Mathf.Min(BakeSize - 1, Mathf.CeilToInt(maxX));
                    int py0 = Mathf.Max(0, Mathf.FloorToInt(minY));
                    int py1 = Mathf.Min(BakeSize - 1, Mathf.CeilToInt(maxY));

                    for (int py = py0; py <= py1; py++)
                    {
                        for (int px = px0; px <= px1; px++)
                        {
                            float cx = px + 0.5f, cy = py + 0.5f;
                            float ba = ((uv1.y - uv2.y) * (cx - uv2.x) + (uv2.x - uv1.x) * (cy - uv2.y)) / denom;
                            float bb = ((uv2.y - uv0.y) * (cx - uv2.x) + (uv0.x - uv2.x) * (cy - uv2.y)) / denom;
                            float bc = 1f - ba - bb;
                            // 稍微放宽（-0.03）做 1px 级溢出，防双线性采样在 UV 岛边缘吸到旧色
                            if (ba < -0.03f || bb < -0.03f || bc < -0.03f)
                            {
                                continue;
                            }

                            float wx = ba * w0.x + bb * w1.x + bc * w2.x;
                            float wz = ba * w0.z + bb * w1.z + bc * w2.z;
                            float gx = (wx - originX) / cellSize;
                            float gz = (wz - originZ) / cellSize;
                            int cellX = Mathf.FloorToInt(gx);
                            int cellZ = Mathf.FloorToInt(gz);

                            string element = map.GetElementIdOrNull(cellX, cellZ, 0);
                            Color32[][] tiles;
                            if (element == "greenField")
                            {
                                tiles = grass; grassPx++;
                            }
                            else if (element == "island")
                            {
                                tiles = dirt; dirtPx++;
                            }
                            else
                            {
                                continue; // floatingZone / 未刷格：保留原贴图
                            }

                            // 格子坐标定块（确定性哈希轮换），格内坐标定块内像素——整块铺贴、界线落在格线上
                            int variant = ((cellX * 73856093) ^ (cellZ * 19349663)) & 0x7fffffff;
                            Color32[] tile = tiles[variant % tiles.Length];
                            float fx = Mathf.Clamp01(gx - cellX);
                            float fz = Mathf.Clamp01(gz - cellZ);
                            outPixels[py * BakeSize + px] = SampleBilinear(tile, TileSampleSize, fx, fz);
                            painted++;
                            cellsHit.Add(((long)cellX << 20) | (uint)cellZ);
                        }
                    }
                }

                // 落盘 + 指材质
                var outTex = new Texture2D(BakeSize, BakeSize, TextureFormat.RGBA32, false);
                outTex.SetPixels32(outPixels);
                File.WriteAllBytes(outPath, outTex.EncodeToPNG());
                Object.DestroyImmediate(outTex);
                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
                var bakedAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);

                int retargeted = RetargetMaterials(assetId, renderer, bakedAsset, log);

                log.AppendLine($"[ok] {assetId}: 覆盖 {cellsHit.Count} 格（草 {grassPx} px / 泥 {dirtPx} px / 共 {painted} px），改 {retargeted} 份材质 → {outPath}");
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        /// <summary>mat/ 正式材质、fbx/Materials 自动材质、渲染器实绑材质三路都指到烘焙图，双保险免疫导入模式歧义。</summary>
        private static int RetargetMaterials(string assetId, Renderer renderer, Texture2D baked, StringBuilder log)
        {
            var targets = new List<Material>();
            var seen = new HashSet<Material>();

            void Add(Material m)
            {
                if (m != null && m.HasProperty("_MainTex") && seen.Add(m))
                {
                    targets.Add(m);
                }
            }

            Add(AssetDatabase.LoadAssetAtPath<Material>($"Assets/Res/{assetId}/mat/{assetId}.mat"));
            Add(AssetDatabase.LoadAssetAtPath<Material>($"Assets/Res/{assetId}/fbx/Materials/texture_diffuse.mat"));
            foreach (Material m in renderer.sharedMaterials)
            {
                Add(m);
            }

            foreach (Material m in targets)
            {
                m.SetTexture("_MainTex", baked);
                EditorUtility.SetDirty(m);
            }
            return targets.Count;
        }

        private static Color32[][] LoadTiles(string prefix, int count)
        {
            var result = new Color32[count][];
            for (int i = 0; i < count; i++)
            {
                string path = $"{TileDir}/{prefix}_{i + 1:00}.png";
                Color32[] px = LoadPngPixels(path, out int w, out int h);
                if (px == null)
                {
                    Debug.LogError($"[烘焙] 读不到贴图块 {path}");
                    return null;
                }
                result[i] = ResizeBilinear(px, w, h, TileSampleSize, TileSampleSize);
            }
            return result;
        }

        /// <summary>绕过 isReadable=0：直接读文件字节解码，不碰 importer。</summary>
        private static Color32[] LoadPngPixels(string path, out int width, out int height)
        {
            width = height = 0;
            if (!File.Exists(path))
            {
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(tex);
                return null;
            }
            width = tex.width;
            height = tex.height;
            Color32[] px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        private static Color32[] ResizeBilinear(Color32[] src, int sw, int sh, int dw, int dh)
        {
            if (sw == dw && sh == dh)
            {
                return src;
            }
            var dst = new Color32[dw * dh];
            for (int y = 0; y < dh; y++)
            {
                float v = (y + 0.5f) / dh;
                for (int x = 0; x < dw; x++)
                {
                    float u = (x + 0.5f) / dw;
                    dst[y * dw + x] = SampleBilinearRect(src, sw, sh, u, v);
                }
            }
            return dst;
        }

        private static Color32 SampleBilinear(Color32[] tile, int size, float u, float v)
        {
            return SampleBilinearRect(tile, size, size, u, v);
        }

        private static Color32 SampleBilinearRect(Color32[] src, int w, int h, float u, float v)
        {
            float fx = Mathf.Clamp(u * w - 0.5f, 0f, w - 1.001f);
            float fy = Mathf.Clamp(v * h - 0.5f, 0f, h - 1.001f);
            int x0 = (int)fx, y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float tx = fx - x0, ty = fy - y0;

            Color32 c00 = src[y0 * w + x0], c10 = src[y0 * w + x1];
            Color32 c01 = src[y1 * w + x0], c11 = src[y1 * w + x1];
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(Mathf.Lerp(c00.r, c10.r, tx), Mathf.Lerp(c01.r, c11.r, tx), ty)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(Mathf.Lerp(c00.g, c10.g, tx), Mathf.Lerp(c01.g, c11.g, tx), ty)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(Mathf.Lerp(c00.b, c10.b, tx), Mathf.Lerp(c01.b, c11.b, tx), ty)),
                255);
        }
    }
}
