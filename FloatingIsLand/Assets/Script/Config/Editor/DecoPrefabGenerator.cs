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
    /// 地图装饰品 Prefab 生成：把 Assets/Res/deco_*/fbx/&lt;id&gt;.fbx 转成
    /// Assets/Resources/Prefab/Deco/&lt;id&gt;.prefab。装饰品不在配表里、没有占地掩码，
    /// 不做按格对齐，只做「归一化」：XZ 以原点为中心、底面 y=0、按类别把最大水平边
    /// 缩到目标世界尺寸（见 <see cref="TargetSizeOf"/>，摆场景的人不用再逐个调缩放）。
    ///
    /// 结构与 <see cref="ModelPrefabGenerator"/> 同款两层（identity 包装根 + FBX 子节点），
    /// 轴向修正/单位换算都留在子节点上，表现层只碰包装根。
    /// </summary>
    public static class DecoPrefabGenerator
    {
        private const string ResRoot = "Assets/Res";
        private const string DecoPrefix = "deco_";
        private const string PrefabDir = "Assets/Resources/Prefab/Deco";

        /// <summary>按 id 前缀配目标尺寸（米，最大水平边）。新装饰品类别在这里补一行。</summary>
        private static readonly (string prefix, float size)[] TargetSizes =
        {
            ("deco_tree", 3.0f),
            ("deco_bush", 1.4f),
            ("deco_boulder", 2.6f),
            ("deco_pillar", 1.8f),
            ("deco_baseRock", 3.2f),
            ("deco_island", 14.0f),
            ("deco_ore", 2.0f),
        };
        private const float DefaultTargetSize = 2.0f;

        [MenuItem("Tools/美术/生成装饰 Prefab（Assets/Res/deco_* → Prefab/Deco）", false, 5)]
        public static void GenerateAll()
        {
            var log = new StringBuilder();
            int made = 0;
            int missing = 0;

            EnsureFolder(PrefabDir);

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string dir in Directory.GetDirectories(ResRoot, DecoPrefix + "*"))
                {
                    string id = Path.GetFileName(dir);
                    string fbx = $"{ResRoot}/{id}/fbx/{id}.fbx";
                    if (!File.Exists(fbx))
                    {
                        log.AppendLine($"  - 缺模型：{id}（{fbx} 不存在）");
                        missing++;
                        continue;
                    }
                    if (Generate(fbx, id, preview, log))
                    {
                        made++;
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[装饰 Prefab] 生成 {made} 个，缺模型 {missing} 个。\n{log}");
        }

        private static float TargetSizeOf(string id)
        {
            foreach (var entry in TargetSizes)
            {
                if (id.StartsWith(entry.prefix, System.StringComparison.Ordinal))
                {
                    return entry.size;
                }
            }
            return DefaultTargetSize;
        }

        private static bool Generate(string fbxPath, string id, Scene workScene, StringBuilder log)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelAsset == null)
            {
                log.AppendLine($"  - 读不到模型：{fbxPath}");
                return false;
            }

            var root = new GameObject(id);
            SceneManager.MoveGameObjectToScene(root, workScene);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, workScene);
                model.transform.SetParent(root.transform, false);

                Bounds bounds;
                if (!TryGetWorldMeshBounds(model, out bounds))
                {
                    log.AppendLine($"  - {id}: 没有网格，跳过");
                    return false;
                }

                float target = TargetSizeOf(id);
                float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
                if (horizontal <= 1e-4f)
                {
                    log.AppendLine($"  - {id}: 包围盒水平退化（{bounds.size.x:0.####}×{bounds.size.z:0.####}m），跳过");
                    return false;
                }
                float scale = target / horizontal;
                model.transform.localScale *= scale;

                // 缩放绕模型轴心，重测后再归位：XZ 居中到原点、底面贴地
                if (!TryGetWorldMeshBounds(model, out bounds))
                {
                    return false;
                }
                model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                string outPath = $"{PrefabDir}/{id}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, outPath);
                log.AppendLine($"  + {outPath}（{bounds.size.x:0.##}×{bounds.size.y:0.##}×{bounds.size.z:0.##}m，目标横边 {target:0.##}m）");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool TryGetWorldMeshBounds(GameObject root, out Bounds bounds)
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
                Vector3[] vertices;
                try
                {
                    vertices = mesh.vertices;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[装饰 Prefab] 读不到 {mesh.name} 的顶点：{e.Message}");
                    continue;
                }
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = toWorld.MultiplyPoint3x4(vertices[i]);
                    if (!any)
                    {
                        bounds = new Bounds(world, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(world);
                    }
                }
            }
            return any;
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
    }
}
