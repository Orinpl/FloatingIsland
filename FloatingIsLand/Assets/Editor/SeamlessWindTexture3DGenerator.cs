using System.IO;
using UnityEditor;
using UnityEngine;

public static class SeamlessWindTexture3DGenerator
{
    private const int Resolution = 32;
    private const string AssetDirectory = "Assets/Resources/Wind";
    private const string AssetPath = AssetDirectory + "/GlobalWindField_Seamless.asset";

    [MenuItem("Tools/Floating Island/Regenerate Seamless 3D Wind Field")]
    public static void GenerateDefaultAsset()
    {
        Directory.CreateDirectory(AssetDirectory);

        var generated = new Texture3D(Resolution, Resolution, Resolution, TextureFormat.RGBA32, false)
        {
            name = "GlobalWindField_Seamless",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 0,
        };

        var voxels = new Color32[Resolution * Resolution * Resolution];
        int index = 0;

        for (int z = 0; z < Resolution; z++)
        {
            float pz = 2f * Mathf.PI * z / Resolution;
            for (int y = 0; y < Resolution; y++)
            {
                float py = 2f * Mathf.PI * y / Resolution;
                for (int x = 0; x < Resolution; x++)
                {
                    float px = 2f * Mathf.PI * x / Resolution;

                    // Every term is periodic over X/Y/Z, so all six volume faces tile continuously.
                    Vector3 direction = new Vector3(
                        1f + 0.22f * Mathf.Sin(py) * Mathf.Cos(pz) + 0.1f * Mathf.Sin(2f * pz + px),
                        0.09f * Mathf.Sin(pz + px) + 0.06f * Mathf.Cos(2f * px - py),
                        0.24f * Mathf.Cos(px) * Mathf.Sin(py) + 0.1f * Mathf.Sin(2f * py - pz)).normalized;

                    float strengthWave =
                        0.55f * Mathf.Sin(px + py) +
                        0.3f * Mathf.Cos(py + pz) +
                        0.15f * Mathf.Sin(pz + px);
                    float strength = Mathf.Clamp01(0.78f + 0.18f * strengthWave);

                    voxels[index++] = new Color(
                        direction.x * 0.5f + 0.5f,
                        direction.y * 0.5f + 0.5f,
                        direction.z * 0.5f + 0.5f,
                        strength);
                }
            }
        }

        generated.SetPixels32(voxels);
        generated.Apply(false, true);

        Texture3D existing = AssetDatabase.LoadAssetAtPath<Texture3D>(AssetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, AssetPath);
        }
        else
        {
            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log($"Generated six-face seamless Texture3D: {AssetPath} ({Resolution}^3 RGBA32)");
    }
}
