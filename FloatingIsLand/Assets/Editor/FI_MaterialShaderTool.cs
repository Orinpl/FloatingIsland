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

        /// <summary>
        /// Everything the audit lists. Wider than <see cref="ConvertRoots"/> on purpose — the
        /// report is allowed to name material this tool will never rewrite, and staying silent
        /// about third-party material on a Built-in shader is how it goes unnoticed until
        /// something renders magenta.
        /// </summary>
        private static readonly string[] MaterialRoots =
        {
            "Assets/Res",
            "Assets/SplitPreview",
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
            var foreign = new List<string>();
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

                if (IsAuthoredShader(material.shader))
                {
                    continue;
                }

                // Split by whether a menu can actually repair it, so the actionable count can
                // reach zero. Lumping the two together would leave the total permanently at 2
                // (third-party material this tool refuses to rewrite), and a count that never
                // reaches zero is a count nobody reads.
                if (IsUnderConvertRoots(path))
                {
                    strays.Add(line);
                }
                else
                {
                    foreign.Add(line);
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

            sb.AppendLine();
            sb.AppendLine(
                $"=== Outside the convert scope, not repaired by any menu: {foreign.Count} ===");
            foreach (string entry in foreign)
            {
                sb.AppendLine(entry);
            }

            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log(
                $"[FI] Material audit written to {ReportPath} " +
                $"({offenders.Count} bad bindings, {strays.Count} assets on a non-project shader, " +
                $"{foreign.Count} outside the convert scope).");
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
        /// Turns the normal map on for every material that actually has one.
        ///
        /// Assigning _BumpMap is not enough: FI_Lit gates the sampling behind the
        /// _NORMALMAP shader_feature, and FI_LitShaderGUI drives that keyword from
        /// _NormalMapGroup. A material with a bump texture but the group left at 0 silently
        /// renders flat, which is easy to mistake for the normal map being broken.
        ///
        /// Deliberately not restricted to FI_Lit the way <see cref="ConvertToFiLit"/> is:
        /// FI_SailWind declares the same _NormalMapGroup / _BumpMap / _NORMALMAP triple, so
        /// the fix is correct for the whole FI shader family. The _NormalMapGroup guard below
        /// is what keeps that from becoming a licence to write into anything at all.
        /// </summary>
        [MenuItem("Tools/FI/Enable Normal Maps On Item Materials")]
        public static void EnableNormalMaps()
        {
            var log = new StringBuilder();
            int changed = 0;
            int skipped = 0;
            int group = Undo.GetCurrentGroup();

            foreach (Material material in CollectConvertibleMaterials())
            {
                string path = AssetDatabase.GetAssetPath(material);

                // _NormalMapGroup has to be checked too, not just _BumpMap. Built-in Standard
                // declares _BumpMap but no _NormalMapGroup, and it reaches this loop now that
                // orphans are in scope — an unconverted mat/ copy is exactly that shape. Without
                // this guard such a material half-applies: SetFloat logs an error and does
                // nothing, while EnableKeyword sticks, because _NORMALMAP is also Standard's own
                // keyword name. The result is a material that claims a normal map it never samples.
                if (string.IsNullOrEmpty(path)
                    || !material.HasProperty("_BumpMap")
                    || !material.HasProperty("_NormalMapGroup"))
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

                Undo.RecordObject(material, "Enable Normal Maps");
                material.SetFloat("_NormalMapGroup", 1f);
                material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
                changed++;
                log.AppendLine($"  {path}");
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[FI] Normal maps enabled on {changed} material(s), {skipped} already ok / no bump map.\n{log}");
        }

        /// <summary>
        /// Everything the material menus should operate on: renderer-bound materials first,
        /// then every material asset under <see cref="ConvertRoots"/> that the renderer walk
        /// did not already reach.
        ///
        /// The order of these two loops is a correctness invariant of <see cref="ConvertToFiLit"/>,
        /// not a preference. Every donor in <see cref="BuildDonorMap"/> is itself one of the
        /// orphans under mat/, and the map holds live Material references that are read lazily
        /// inside the conversion loop. Visiting renderer-bound materials first guarantees each
        /// consumer reads its donor while the donor is still in its authored state. Flip the
        /// loops and donors get converted to FI_Lit-with-defaults first, the shader check on
        /// the donor then passes, and every consumer is "seeded" from defaults — the tuned look
        /// is lost and the run reports success, because the seeded counter cannot tell the
        /// difference.
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
            foreach (string guid in AssetDatabase.FindAssets("t:Model", ConvertRoots))
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
