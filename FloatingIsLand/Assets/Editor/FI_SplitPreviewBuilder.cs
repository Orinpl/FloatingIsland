using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// Builds a preview of the split models under Assets/SplitPreview.
    ///
    /// The split FBXs are staged here rather than swapped into Assets/Res, so they need the
    /// same importer setup the real assets use: legacy External Materials + "name from base
    /// texture" + recursive-up search, which makes them bind to Assets/SplitPreview/Materials.
    /// Anything else and they import as untextured white.
    ///
    /// The preview prefab shows every model twice — assembled, and with the split child pulled
    /// aside — so the partition is visible at a glance without entering play mode.
    /// </summary>
    public static class FI_SplitPreviewBuilder
    {
        private const string PreviewDir = "Assets/SplitPreview";
        private const string PrefabPath = PreviewDir + "/SplitPreview.prefab";
        private const float TargetHeight = 4f;
        private const float Spacing = 6f;

        private static readonly string[] Assets = { "giantWindmill", "windmill", "sail" };

        [MenuItem("Tools/FI/Build Split Preview")]
        public static void Build()
        {
            var log = new StringBuilder();

            foreach (string asset in Assets)
            {
                string path = $"{PreviewDir}/{asset}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogError($"[FI] no ModelImporter at {path}");
                    return;
                }

                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.materialLocation = ModelImporterMaterialLocation.External;
                importer.materialName = ModelImporterMaterialName.BasedOnTextureName;
                importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
                importer.SaveAndReimport();
            }

            Scene preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("SplitPreview");
            SceneManager.MoveGameObjectToScene(root, preview);

            for (int i = 0; i < Assets.Length; i++)
            {
                string asset = Assets[i];
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{PreviewDir}/{asset}.fbx");
                if (model == null)
                {
                    Debug.LogError($"[FI] could not load {asset}.fbx");
                    continue;
                }

                var column = new GameObject(asset);
                column.transform.SetParent(root.transform, false);
                column.transform.localPosition = new Vector3(i * Spacing * 2f, 0f, 0f);

                GameObject whole = Place(model, column.transform, Vector3.zero, $"{asset}_whole", log);
                GameObject apart = Place(model, column.transform, new Vector3(Spacing, 0f, 0f),
                    $"{asset}_split", log);
                Explode(apart, log);

                log.AppendLine($"  {asset}: whole={Describe(whole)} | split={Describe(apart)}");
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(preview);
            AssetDatabase.Refresh();

            Debug.Log($"[FI] Split preview built at {PrefabPath}\n{log}");
        }

        /// <summary>Instantiate and normalise to a readable size so all three sit side by side.</summary>
        private static GameObject Place(
            GameObject model, Transform parent, Vector3 localPos, string name, StringBuilder log)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = name;
            instance.transform.localPosition = localPos;

            Bounds bounds;
            if (TryGetBounds(instance, out bounds) && bounds.size.y > 1e-6f)
            {
                float scale = TargetHeight / bounds.size.y;
                instance.transform.localScale *= scale;
                if (TryGetBounds(instance, out bounds))
                {
                    // sit it on the floor, centred on its slot
                    Vector3 offset = instance.transform.position - bounds.center;
                    offset.y = instance.transform.position.y - bounds.min.y;
                    instance.transform.position += offset;
                }
            }
            else
            {
                log.AppendLine($"  [warn] {name} has no renderable bounds");
            }

            return instance;
        }

        /// <summary>Pull the split child aside so the partition reads at a glance.</summary>
        private static void Explode(GameObject instance, StringBuilder log)
        {
            Transform child = FindSplitChild(instance.transform);
            if (child == null)
            {
                log.AppendLine($"  [warn] {instance.name}: no split child found");
                return;
            }

            Bounds bounds;
            float lift = TryGetBounds(instance, out bounds) ? bounds.size.y * 0.55f : 2f;
            child.position += new Vector3(0f, lift, 0f);
        }

        private static Transform FindSplitChild(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root)
                {
                    continue;
                }

                string n = t.name.ToLowerInvariant();
                if (n.Contains("blade") || n.Contains("cloth"))
                {
                    return t;
                }
            }

            return null;
        }

        private static string Describe(GameObject instance)
        {
            var parts = new List<string>();
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                parts.Add($"{r.name}[{(m == null ? "NO MATERIAL" : m.shader.name)}]");
            }

            return string.Join(" + ", parts);
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return any;
        }
    }
}
