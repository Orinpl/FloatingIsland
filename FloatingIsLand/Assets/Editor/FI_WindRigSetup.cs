using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using FloatingIsLand.View.Environment;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// Authors the wind rig onto the generated prefabs: blade rotators on the two windmills,
    /// and the FI/Sail Wind material on the sail's cloth node.
    ///
    /// This has to run AFTER Tools/美术/生成白模 Prefab, because that menu rebuilds the prefabs
    /// from scratch and drops anything added here. Re-running is safe — every step is idempotent.
    /// </summary>
    public static class FI_WindRigSetup
    {
        private const string SailWindShaderPath = "Assets/Resources/Shaders/FI_SailWind.shader";
        private const string SailMaterialPath = "Assets/Res/sail/fbx/Materials/sail_texture_diffuse.mat";
        private const string ClothMaterialPath = "Assets/Res/sail/fbx/Materials/sail_cloth_wind.mat";
        private const string SailPrefabPath = "Assets/Resources/Prefab/Building/sail_01.prefab";
        private const string ReportPath = "Temp/fi_wind_rig.txt";

        private struct WindmillRig
        {
            public string PrefabPath;
            public string BladeNode;
            public float Rpm;
        }

        private static readonly WindmillRig[] Windmills =
        {
            new WindmillRig
            {
                PrefabPath = "Assets/Resources/Prefab/Building/windmill_01.prefab",
                BladeNode = "windmill_blade",
                Rpm = 6f,
            },
            new WindmillRig
            {
                // the giant one reads as much heavier, so it turns slower than the small mill
                PrefabPath = "Assets/Resources/Prefab/Element/giantWindmill.prefab",
                BladeNode = "giantWindmill_blade",
                Rpm = 3f,
            },
        };

        [MenuItem("Tools/FI/Rig Wind Prefabs")]
        public static void Rig()
        {
            var log = new StringBuilder();

            foreach (WindmillRig rig in Windmills)
            {
                RigWindmill(rig, log);
            }

            RigSail(log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            System.IO.File.WriteAllText(ReportPath, log.ToString());
            Debug.Log($"[FI] Wind rig setup written to {ReportPath}\n{log}");
        }

        private static void RigWindmill(WindmillRig rig, StringBuilder log)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(rig.PrefabPath);
            if (root == null)
            {
                log.AppendLine($"  [err] cannot load {rig.PrefabPath}");
                return;
            }

            try
            {
                Transform blade = FindDeep(root.transform, rig.BladeNode);
                if (blade == null)
                {
                    log.AppendLine($"  [err] {rig.PrefabPath}: no node named {rig.BladeNode}");
                    return;
                }

                var filter = blade.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    log.AppendLine($"  [err] {rig.BladeNode} has no mesh");
                    return;
                }

                string axisReport;
                WindmillBladeRotator.LocalAxis axis = DetectSpinAxis(filter.sharedMesh, out axisReport);

                var rotator = blade.GetComponent<WindmillBladeRotator>();
                if (rotator == null)
                {
                    rotator = blade.gameObject.AddComponent<WindmillBladeRotator>();
                }

                var so = new SerializedObject(rotator);
                so.FindProperty("axis").enumValueIndex = (int)axis;
                so.FindProperty("rpm").floatValue = rig.Rpm;
                so.FindProperty("randomizeStartAngle").boolValue = true;
                so.FindProperty("rotateInEditMode").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, rig.PrefabPath);
                log.AppendLine($"  {rig.BladeNode}: rotator axis={axis} rpm={rig.Rpm}  |  {axisReport}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RigSail(StringBuilder log)
        {
            Material cloth = EnsureClothMaterial(log);
            if (cloth == null)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(SailPrefabPath);
            if (root == null)
            {
                log.AppendLine($"  [err] cannot load {SailPrefabPath}");
                return;
            }

            try
            {
                Transform node = FindDeep(root.transform, "sail_cloth");
                if (node == null)
                {
                    log.AppendLine("  [err] sail_01: no node named sail_cloth");
                    return;
                }

                var renderer = node.GetComponent<Renderer>();
                var filter = node.GetComponent<MeshFilter>();
                if (renderer == null || filter == null || filter.sharedMesh == null)
                {
                    log.AppendLine("  [err] sail_cloth has no renderer/mesh");
                    return;
                }

                // FI_SailWind reads its wind UV from uv2; without that channel the mask is a
                // constant zero and the cloth never moves, so this is worth shouting about.
                Mesh mesh = filter.sharedMesh;
                bool hasUv2 = mesh.uv2 != null && mesh.uv2.Length == mesh.vertexCount && mesh.vertexCount > 0;
                log.AppendLine($"  sail_cloth mesh: verts={mesh.vertexCount} uv2={(hasUv2 ? "present" : "MISSING")}");
                if (!hasUv2)
                {
                    log.AppendLine("  [warn] cloth has no uv2 — FI/Sail Wind will not animate it.");
                }

                var materials = new List<Material>();
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    materials.Add(cloth);
                }

                renderer.sharedMaterials = materials.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, SailPrefabPath);
                log.AppendLine($"  sail_cloth: material -> {cloth.name} ({cloth.shader.name})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Cloth needs its own material: the FBX gives both nodes the same one.</summary>
        private static Material EnsureClothMaterial(StringBuilder log)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SailWindShaderPath);
            if (shader == null)
            {
                log.AppendLine($"  [err] missing shader {SailWindShaderPath}");
                return null;
            }

            var source = AssetDatabase.LoadAssetAtPath<Material>(SailMaterialPath);
            var cloth = AssetDatabase.LoadAssetAtPath<Material>(ClothMaterialPath);
            if (cloth == null)
            {
                cloth = new Material(shader);
                AssetDatabase.CreateAsset(cloth, ClothMaterialPath);
                log.AppendLine($"  created {ClothMaterialPath}");
            }

            // inherit the tuned FI_Lit look, then switch to the wind shader (a superset)
            if (source != null)
            {
                cloth.shader = shader;
                cloth.CopyPropertiesFromMaterial(source);
                cloth.shader = shader;
                cloth.shaderKeywords = source.shaderKeywords;
                cloth.renderQueue = -1;
            }

            // the sail is a single-sided sheet: culling it would make the back face vanish
            if (cloth.HasProperty("_Cull"))
            {
                cloth.SetFloat("_Cull", 0f);
            }

            EditorUtility.SetDirty(cloth);
            return cloth;
        }

        /// <summary>
        /// Which local axis the blade spins about, decided from the mesh instead of hard-coded.
        /// A fixed rule would not cover both rigs: the small mill's blades lie in a plane so its
        /// axis is the THIN one, while the giant turbine is a tall vertical rotor whose axis is
        /// the LONG one. What both share is rotational symmetry, so the winning axis is the one
        /// whose bounding box changes least when the mesh is spun about it.
        /// </summary>
        private static WindmillBladeRotator.LocalAxis DetectSpinAxis(Mesh mesh, out string report)
        {
            Vector3[] verts = mesh.vertices;
            float[] angles = { 45f, 90f, 135f, 180f };
            Vector3 baseSize = SizeOf(verts, Quaternion.identity);

            var scores = new float[3];
            for (int a = 0; a < 3; a++)
            {
                Vector3 axisVec = a == 0 ? Vector3.right : (a == 1 ? Vector3.up : Vector3.forward);
                float total = 0f;
                foreach (float angle in angles)
                {
                    Vector3 size = SizeOf(verts, Quaternion.AngleAxis(angle, axisVec));
                    total += (size - baseSize).magnitude;
                }

                scores[a] = total / (angles.Length * Mathf.Max(baseSize.magnitude, 1e-6f));
            }

            int best = 0;
            for (int a = 1; a < 3; a++)
            {
                if (scores[a] < scores[best])
                {
                    best = a;
                }
            }

            report = $"axis scores X={scores[0]:0.0000} Y={scores[1]:0.0000} Z={scores[2]:0.0000} (lower = more symmetric)";
            return best == 0
                ? WindmillBladeRotator.LocalAxis.X
                : (best == 1 ? WindmillBladeRotator.LocalAxis.Y : WindmillBladeRotator.LocalAxis.Z);
        }

        private static Vector3 SizeOf(Vector3[] verts, Quaternion rotation)
        {
            Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = rotation * verts[i];
                mn = Vector3.Min(mn, p);
                mx = Vector3.Max(mx, p);
            }

            return mx - mn;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
