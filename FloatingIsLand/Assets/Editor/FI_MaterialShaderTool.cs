using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// Audits and converts the shader used by the project's model materials.
    /// The audit walks the actual renderer bindings instead of the .mat files:
    /// the FBX importers run in legacy "External Materials" mode, so their
    /// externalObjects remap is ignored and the materials next to the model win.
    /// </summary>
    public static class FI_MaterialShaderTool
    {
        private const string FiLitPath = "Assets/Resources/Shaders/FI_Lit.shader";
        private const string ReportPath = "Temp/fi_material_audit.txt";

        private static readonly string[] PrefabRoots =
        {
            "Assets/Resources/Prefab",
            "Assets/SplitPreview",
        };

        private static readonly string[] MaterialRoots =
        {
            "Assets/Res",
            "Assets/cicheng",
        };

        [MenuItem("Tools/FI/Audit Material Shaders")]
        public static void Audit()
        {
            var sb = new StringBuilder();
            var offenders = new List<string>();

            sb.AppendLine("=== Renderer bindings in prefabs ===");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material material = materials[i];
                        string shaderName = material == null ? "<NULL MATERIAL>" : material.shader.name;
                        string materialPath = material == null
                            ? "-"
                            : (AssetDatabase.GetAssetPath(material) ?? "<embedded>");
                        string line =
                            $"{shaderName,-24} | {path} > {renderer.name}[{i}] " +
                            $"= {(material == null ? "-" : material.name)} ({materialPath})";
                        sb.AppendLine(line);
                        if (shaderName != "FI_Lit")
                        {
                            offenders.Add(line);
                        }
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== Material assets ===");
            foreach (string guid in AssetDatabase.FindAssets("t:Material", MaterialRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                sb.AppendLine($"{material.shader.name,-24} | {path}");
            }

            sb.AppendLine();
            sb.AppendLine($"=== Non-FI_Lit renderer bindings: {offenders.Count} ===");
            foreach (string offender in offenders)
            {
                sb.AppendLine(offender);
            }

            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log($"[FI] Material audit written to {ReportPath} ({offenders.Count} non-FI_Lit bindings).");
        }

        /// <summary>
        /// Converts every material a prefab renderer actually binds to FI_Lit.
        /// Tuned FI_Lit values are inherited from the material the owning FBX
        /// remaps the same slot name to, so the authored look is not reset to
        /// shader defaults. The converted material keeps its own textures.
        /// </summary>
        [MenuItem("Tools/FI/Convert Item Materials To FI_Lit")]
        public static void ConvertToFiLit()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(FiLitPath);
            if (shader == null)
            {
                Debug.LogError($"[FI] Shader not found at {FiLitPath}.");
                return;
            }

            Dictionary<Material, Material> donors = BuildDonorMap();
            var log = new StringBuilder();
            int converted = 0;
            int seeded = 0;

            foreach (Material material in CollectBoundMaterials())
            {
                string path = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(path) || material.shader == shader)
                {
                    continue;
                }

                Texture mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                Texture bumpMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;

                Undo.RecordObject(material, "Convert To FI_Lit");
                material.shader = shader;

                donors.TryGetValue(material, out Material donor);
                if (donor != null && donor.shader == shader)
                {
                    material.CopyPropertiesFromMaterial(donor);
                    material.shaderKeywords = donor.shaderKeywords;
                    seeded++;
                }
                else
                {
                    material.EnableKeyword("_IBL_USE_BUILTIN");
                    material.EnableKeyword("_SMOOTH_NORMAL_OFF");
                }

                // Keep this material's own textures: the donor points at duplicate
                // copies under mat/, these point at the .fbm set the FBX ships with.
                if (mainTex != null)
                {
                    material.SetTexture("_MainTex", mainTex);
                }

                if (bumpMap != null)
                {
                    material.SetTexture("_BumpMap", bumpMap);
                    material.SetFloat("_NormalMapGroup", 1f);
                    material.EnableKeyword("_NORMALMAP");
                }

                // Follow whatever queue FI_Lit declares rather than pinning one.
                material.renderQueue = -1;
                EditorUtility.SetDirty(material);
                converted++;
                log.AppendLine($"  {path}   <- {(donor != null ? AssetDatabase.GetAssetPath(donor) : "shader defaults")}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FI] Converted {converted} material(s) to FI_Lit ({seeded} seeded from tuned donors):\n{log}");
        }

        /// <summary>All materials referenced by a renderer under <see cref="PrefabRoots"/>.</summary>
        private static List<Material> CollectBoundMaterials()
        {
            var seen = new HashSet<Material>();
            var result = new List<Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabRoots))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null)
                {
                    continue;
                }

                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null && seen.Add(material))
                        {
                            result.Add(material);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Maps each material sitting next to an FBX to the material that FBX's
        /// externalObjects remap assigns to the same slot name.
        /// </summary>
        private static Dictionary<Material, Material> BuildDonorMap()
        {
            var map = new Dictionary<Material, Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Res" }))
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                string materialsDir = Path.GetDirectoryName(fbxPath).Replace('\\', '/') + "/Materials";
                foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> entry in
                         importer.GetExternalObjectMap())
                {
                    if (!(entry.Value is Material donor))
                    {
                        continue;
                    }

                    var local = AssetDatabase.LoadAssetAtPath<Material>(
                        $"{materialsDir}/{entry.Key.name}.mat");
                    if (local != null && local != donor)
                    {
                        map[local] = donor;
                    }
                }
            }

            return map;
        }
    }
}
