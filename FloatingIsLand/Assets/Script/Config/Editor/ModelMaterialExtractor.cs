using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// 把 Assets/Res/&lt;资产名&gt;/fbx/*.fbx 里内嵌的贴图与材质提取成独立文件，落到同级 mat/ 目录并回挂给 FBX。
    ///
    /// 为什么需要这一步：FBX 里的贴图是「内嵌」的（rodin 生成的模型自带 diffuse/normal/roughness/metallic
    /// 四张图），Unity 导入内嵌贴图**不会自动展开**——只建材质、贴图槽留空，所以模型看起来是白模。
    /// 等价于在 Inspector 的 Materials 页手点 Extract Textures + Extract Materials，这里做成批量。
    ///
    /// 提取后 FBX 的 externalObjects 会指向 mat/ 下的真实文件，材质可以自由改颜色/换 shader，
    /// 也不会在每次重新导入模型时被覆盖。
    /// </summary>
    public static class ModelMaterialExtractor
    {
        private const string ResRoot = "Assets/Res";

        [MenuItem("Tools/美术/提取模型材质与贴图（FBX → mat/）", false, 40)]
        public static void ExtractAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ResRoot });
            var log = new StringBuilder();
            int done = 0;
            int skipped = 0;

            // 注意：这里**不能**包 StartAssetEditing/StopAssetEditing。
            // 那会把导入全部延迟到 Stop 之后，而 ExtractAsset 依赖「写完 remap 立刻重新导入 FBX」
            // 才能把材质绑到渲染器上；批处理里做完，externalObjects 是对的、导入结果却还是旧的，
            // 表现为运行时渲染器上挂着 Default-Material（踩过一次，别再包回去）。
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string fbxPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("提取材质与贴图", fbxPath, (float)i / Mathf.Max(1, guids.Length));
                    if (ExtractOne(fbxPath, log))
                    {
                        done++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            // 贴图落盘后再统一修一遍：法线图要标成 Normal map，并把贴图按语义挂到材质槽上
            var fixedUp = 0;
            foreach (string guid in guids)
            {
                if (AssignTextures(AssetDatabase.GUIDToAssetPath(guid), log))
                {
                    fixedUp++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[材质提取] 提取 {done} 个模型（跳过 {skipped}），回挂贴图 {fixedUp} 个。\n{log}");

            ReimportAndVerify();
        }

        /// <summary>
        /// 强制重新导入 Assets/Res 下的模型，并逐个报告渲染器上真正绑到的材质。
        ///
        /// externalObjects 里写着重映射不等于导入结果已经生效——Library 里缓存的导入结果不会自己更新。
        /// 报告里出现 Default-Material 就说明这个模型的材质没绑上（Unity 找不到材质时的替身）。
        /// </summary>
        [MenuItem("Tools/美术/重新导入模型并校验材质绑定", false, 41)]
        public static void ReimportAndVerify()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ResRoot });
            foreach (string guid in guids)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }

            var log = new StringBuilder();
            int bad = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    continue;
                }

                foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        string name = material == null ? "<无材质>" : material.name;
                        bool ok = material != null && name != "Default-Material";
                        if (!ok)
                        {
                            bad++;
                            log.AppendLine($"  X {Path.GetFileNameWithoutExtension(path)} → {name}");
                        }
                    }
                }
            }

            if (bad > 0)
            {
                Debug.LogError($"[材质校验] {guids.Length} 个模型里有 {bad} 处没绑上材质：\n{log}");
            }
            else
            {
                Debug.Log($"[材质校验] {guids.Length} 个模型的材质全部绑定正常。");
            }
        }

        /// <summary>提取单个 FBX 的贴图与材质到同级 mat/ 目录。</summary>
        private static bool ExtractOne(string fbxPath, StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            // Assets/Res/<id>/fbx/<id>.fbx → Assets/Res/<id>/mat
            string fbxDir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            string assetDir = Path.GetDirectoryName(fbxDir).Replace('\\', '/');
            string matDir = assetDir + "/mat";
            string assetId = Path.GetFileNameWithoutExtension(fbxPath);

            if (!AssetDatabase.IsValidFolder(matDir))
            {
                AssetDatabase.CreateFolder(assetDir, "mat");
            }

            // 1) 内嵌贴图 → 文件。ExtractTextures 内部会把 externalObjects 指到提取出的贴图。
            bool extracted = importer.ExtractTextures(matDir);

            // 2) 内嵌材质 → 文件。材质名统一是 "model"，按资产名重命名，避免以后混在一起分不清。
            importer.materialLocation = ModelImporterMaterialLocation.External;
            AssetDatabase.WriteImportSettingsIfDirty(fbxPath);
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

            int matCount = 0;
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                var material = sub as Material;
                if (material == null)
                {
                    continue;
                }
                string dest = $"{matDir}/{assetId}.mat";
                if (matCount > 0)
                {
                    dest = $"{matDir}/{assetId}_{matCount}.mat";
                }
                string error = AssetDatabase.ExtractAsset(material, dest);
                if (!string.IsNullOrEmpty(error))
                {
                    log.AppendLine($"  - {assetId}: 提取材质失败 {error}");
                }
                matCount++;
            }

            AssetDatabase.WriteImportSettingsIfDirty(fbxPath);

            // 提取会在 FBX 旁边留下两份没人引用的副本，清掉（实测删掉后重新导入不会再生成）：
            //   <name>.fbm/   Unity 解包内嵌媒体的落脚点，贴图已经在 mat/ 有一份
            //   Materials/    materialLocation=External 时 Unity 自动生成的材质，已被 externalObjects 顶掉
            DeleteFolder($"{fbxDir}/{assetId}.fbm");
            DeleteFolder($"{fbxDir}/Materials");

            log.AppendLine($"  - {assetId}: 贴图提取{(extracted ? "成功" : "无内嵌贴图")}，材质 {matCount} 个 → {matDir}");
            return true;
        }

        private static void DeleteFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        /// <summary>
        /// 把 mat/ 下的贴图按文件名语义挂到材质槽（Standard shader），并把法线图标成 Normal map。
        /// Unity 的 FBX 材质描述只可靠地映射 DiffuseColor/NormalMap，roughness/metallic 这类
        /// 要自己接；这里按 rodin 的固定命名（texture_diffuse / _normal / _metallic / _roughness）匹配。
        /// </summary>
        private static bool AssignTextures(string fbxPath, StringBuilder log)
        {
            string fbxDir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            string matDir = Path.GetDirectoryName(fbxDir).Replace('\\', '/') + "/mat";
            if (!AssetDatabase.IsValidFolder(matDir))
            {
                return false;
            }

            var byKind = new Dictionary<string, Texture2D>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { matDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                {
                    continue;
                }

                if (name.Contains("normal"))
                {
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti != null && ti.textureType != TextureImporterType.NormalMap)
                    {
                        ti.textureType = TextureImporterType.NormalMap;
                        ti.SaveAndReimport();
                    }
                    byKind["normal"] = tex;
                }
                else if (name.Contains("diffuse") || name.Contains("basecolor") || name.Contains("base_color") || name.Contains("albedo"))
                {
                    byKind["diffuse"] = tex;
                }
                else if (name.Contains("metallic"))
                {
                    byKind["metallic"] = tex;
                }
                else if (name.Contains("roughness"))
                {
                    byKind["roughness"] = tex;
                }
            }

            if (byKind.Count == 0)
            {
                return false;
            }

            bool touched = false;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { matDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                Texture2D diffuse;
                if (byKind.TryGetValue("diffuse", out diffuse))
                {
                    SetTexture(material, diffuse, "_MainTex", "_BaseMap");
                    // 贴图接上后底色要恢复成白，否则会和材质自带的颜色相乘变暗/偏色
                    if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", Color.white);
                    }
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", Color.white);
                    }
                    touched = true;
                }

                Texture2D normal;
                if (byKind.TryGetValue("normal", out normal))
                {
                    SetTexture(material, normal, "_BumpMap", "_NormalMap");
                    material.EnableKeyword("_NORMALMAP");
                    touched = true;
                }

                Texture2D metallic;
                if (byKind.TryGetValue("metallic", out metallic))
                {
                    // Standard 的 _MetallicGlossMap 是打包图（R=metallic，A=smoothness），
                    // rodin 给的是分离的 metallic/roughness；直接挂 metallic 图取其 R 通道即可，
                    // smoothness 用统一系数控制（low poly 风格不需要逐像素粗糙度）。
                    SetTexture(material, metallic, "_MetallicGlossMap");
                    material.EnableKeyword("_METALLICGLOSSMAP");
                    touched = true;
                }
                if (material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness", 0.2f);
                }
                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", 0.2f);
                }

                if (touched)
                {
                    EditorUtility.SetDirty(material);
                    log.AppendLine($"  - 回挂贴图：{path}（diffuse={byKind.ContainsKey("diffuse")} normal={byKind.ContainsKey("normal")} metallic={byKind.ContainsKey("metallic")}）");
                }
            }

            return touched;
        }

        private static void SetTexture(Material material, Texture texture, params string[] candidateProperties)
        {
            foreach (string prop in candidateProperties)
            {
                if (material.HasProperty(prop))
                {
                    material.SetTexture(prop, texture);
                    return;
                }
            }
        }
    }
}
