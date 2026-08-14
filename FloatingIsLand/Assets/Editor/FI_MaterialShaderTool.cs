using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// Audits and converts the shader used by the project's model materials.
    ///
    /// The audit reports renderer bindings rather than only the .mat files, because the FBX
    /// importers run in legacy "External Materials" mode: their externalObjects remap is
    /// ignored and the materials sitting next to the model win. What a renderer binds is
    /// therefore the only reliable answer to "which material actually renders".
    ///
    /// Conversion, however, must NOT stop at renderer bindings. A material that no renderer
    /// binds today is one FBX reimport away from binding — legacy External mode regenerates
    /// the model's Materials folder and re-resolves by name, and the remap targets under
    /// mat/ become live the moment the importer mode is changed. Such a material is invisible
    /// to a renderer walk, so it silently keeps whatever shader it was born with. That is how
    /// Assets/Res/dock/mat/dock.mat rode a Built-in Standard shader through the switch to URP,
    /// where Standard has no usable SubShader, and had to be patched by hand. Conversion works
    /// off <see cref="CollectConvertibleMaterials"/>, which is the union of both sets.
    /// </summary>
    public static class FI_MaterialShaderTool
    {
        private const string FiLitPath = "Assets/Resources/Shaders/FI_Lit.shader";
        private const string ReportPath = "Temp/fi_material_audit.txt";

        /// <summary>
        /// Where the project's own shaders live. A material already on one of these was put
        /// there deliberately — the sail's cloth belongs on FI/Sail Wind, not FI_Lit — so
        /// conversion skips it. Testing the shader's folder rather than listing shader names
        /// means a newly authored project shader is protected without touching this file.
        /// </summary>
        private const string AuthoredShaderDir = "Assets/Resources/Shaders/";

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

        /// <summary>
        /// Roots where every material asset is expected to sit on a project shader.
        /// Deliberately narrower than <see cref="MaterialRoots"/>: Assets/cicheng is
        /// third-party and only ever listed by the audit, never rewritten.
        /// </summary>
        private static readonly string[] ConvertRoots =
        {
            "Assets/Res",
        };

        [MenuItem("Tools/FI/Audit Material Shaders")]
        public static void Audit()
        {
            var sb = new StringBuilder();
            var offenders = new List<string>();
            var strays = new List<string>();
            var bound = new HashSet<Material>();

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
                        if (material != null)
                        {
                            bound.Add(material);
                        }

                        string shaderName = material == null ? "<NULL MATERIAL>" : material.shader.name;
                        string materialPath = material == null
                            ? "-"
                            : (AssetDatabase.GetAssetPath(material) ?? "<embedded>");
                        string line =
                            $"{shaderName,-24} | {path} > {renderer.name}[{i}] " +
                            $"= {(material == null ? "-" : material.name)} ({materialPath})";
                        sb.AppendLine(line);

                        // Any project shader is a legitimate binding, not just FI_Lit: the wind
                        // rig deliberately puts the sail's cloth on FI/Sail Wind. Flagging that
                        // as an offender every run is how a report stops being read.
                        if (material == null || !IsAuthoredShader(material.shader))
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

                // "orphan" = no renderer binds it, so the section above cannot see it at all.
                bool orphan = !bound.Contains(material);
                string line = $"{material.shader.name,-24} | {path}{(orphan ? "   [orphan]" : string.Empty)}";
                sb.AppendLine(line);

                if (IsUnderConvertRoots(path) && !IsAuthoredShader(material.shader))
                {
                    strays.Add(line);
                }
            }

            sb.AppendLine();
            sb.AppendLine($"=== Renderer bindings on a non-project shader: {offenders.Count} ===");
            foreach (string offender in offenders)
            {
                sb.AppendLine(offender);
            }

            // Counted separately from the bindings above because the failure mode differs:
            // these render correctly today only because nothing draws them yet.
            sb.AppendLine();
            sb.AppendLine($"=== Material assets on a non-project shader: {strays.Count} ===");
            foreach (string stray in strays)
            {
                sb.AppendLine(stray);
            }

            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log(
                $"[FI] Material audit written to {ReportPath} " +
                $"({offenders.Count} bad bindings, {strays.Count} assets on a non-project shader).");
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

            foreach (Material material in CollectConvertibleMaterials())
            {
                string path = AssetDatabase.GetAssetPath(material);

                // Skipping every project shader, not just FI_Lit: a material already on
                // FI/Sail Wind is there because the wind rig put it there, and stamping
                // FI_Lit over it would silently kill the sail animation.
                if (string.IsNullOrEmpty(path) || IsAuthoredShader(material.shader))
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

        /// <summary>
        /// Turns the normal map on for every bound material that actually has one.
        ///
        /// Assigning _BumpMap is not enough: FI_Lit gates the sampling behind the
        /// _NORMALMAP shader_feature, and FI_LitShaderGUI drives that keyword from
        /// _NormalMapGroup. A material with a bump texture but the group left at 0 silently
        /// renders flat, which is easy to mistake for the normal map being broken.
        /// </summary>
        [MenuItem("Tools/FI/Enable Normal Maps On Item Materials")]
        public static void EnableNormalMaps()
        {
            var log = new StringBuilder();
            int changed = 0;
            int skipped = 0;

            foreach (Material material in CollectConvertibleMaterials())
            {
                string path = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(path) || !material.HasProperty("_BumpMap"))
                {
                    continue;
                }

                if (material.GetTexture("_BumpMap") == null)
                {
                    skipped++;
                    continue;
                }

                bool alreadyOn = material.GetFloat("_NormalMapGroup") > 0.5f
                                 && material.IsKeywordEnabled("_NORMALMAP");
                if (alreadyOn)
                {
                    skipped++;
                    continue;
                }

                material.SetFloat("_NormalMapGroup", 1f);
                material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
                changed++;
                log.AppendLine($"  {path}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FI] Normal maps enabled on {changed} material(s), {skipped} already ok / no bump map.\n{log}");
        }

        /// <summary>
        /// Everything the material menus should operate on: renderer-bound materials first,
        /// then every material asset under <see cref="ConvertRoots"/> that the renderer walk
        /// did not already reach.
        ///
        /// Order matters for <see cref="ConvertToFiLit"/>. Renderer-bound materials come first
        /// because they are the ones with a tuned donor to inherit from; the orphans that
        /// follow are usually those donors' own duplicates under mat/, which have nothing to
        /// inherit and land on shader defaults either way.
        /// </summary>
        private static List<Material> CollectConvertibleMaterials()
        {
            var seen = new HashSet<Material>();
            var result = new List<Material>();

            foreach (Material material in CollectBoundMaterials())
            {
                if (seen.Add(material))
                {
                    result.Add(material);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", ConvertRoots))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material != null && seen.Add(material))
                {
                    result.Add(material);
                }
            }

            return result;
        }

        /// <summary>Is this shader one the project authored, as opposed to a built-in or package one?</summary>
        private static bool IsAuthoredShader(Shader shader)
        {
            if (shader == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(shader);
            return !string.IsNullOrEmpty(path)
                   && path.StartsWith(AuthoredShaderDir, System.StringComparison.Ordinal);
        }

        private static bool IsUnderConvertRoots(string assetPath)
        {
            for (int i = 0; i < ConvertRoots.Length; i++)
            {
                if (assetPath.StartsWith(ConvertRoots[i] + "/", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
