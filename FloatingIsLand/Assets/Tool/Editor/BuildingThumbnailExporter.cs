using System.Collections.Generic;
using System.IO;
using System.Text;
using FloatingIsLand.Config;
using FloatingIsLand.UI;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// 把 <see cref="BuildingThumbnail"/> 现渲的略缩图导出成 PNG，用来在编辑器里直接检查
    /// 「手牌上那张小图长什么样」——不用进 Play、不用把鼠标停在按钮上。
    ///
    /// 走的是**运行时那份**渲染代码，不是另写一套：另写一套的话导出图好看、游戏里不好看，
    /// 排查反而更费劲。模型缺材质 / 贴图丢失这类问题在这里一眼可见。
    /// </summary>
    public static class BuildingThumbnailExporter
    {
        private const string OutputDir = "Temp/Thumbnails";

        [MenuItem("Tools/美术/导出建筑略缩图（排查用）", false, 40)]
        public static void Export()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources("Tables");
            }

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDir));
            Directory.CreateDirectory(root);

            var report = new StringBuilder();
            var missing = new List<string>();
            int written = 0;

            foreach (BuildingVariantRow variant in Tables.BuildingVariant.All)
            {
                Texture texture = BuildingThumbnail.Get(variant.variantId, variant.prefabPath);
                var rt = texture as RenderTexture;
                if (rt == null)
                {
                    missing.Add(variant.variantId);
                    continue;
                }

                string path = Path.Combine(root, variant.variantId + ".png");
                File.WriteAllBytes(path, Encode(rt));
                written++;
                report.AppendLine($"  {variant.variantId} → {path}");
            }

            var message = new StringBuilder();
            message.AppendLine($"[略缩图] 已导出 {written} 张 → {root}");
            if (missing.Count > 0)
            {
                message.AppendLine($"[略缩图] 以下 {missing.Count} 个变体没有模型（prefabPath 为空或资源缺失），" +
                                   $"手牌上只会显示占地图标：{string.Join("、", missing)}");
            }
            message.Append(report);
            Debug.Log(message.ToString());
        }

        private static byte[] Encode(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;

            var readback = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
            readback.Apply();

            RenderTexture.active = previous;

            byte[] png = readback.EncodeToPNG();
            Object.DestroyImmediate(readback);
            return png;
        }
    }
}
