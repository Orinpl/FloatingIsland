using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// Assets/Res 下每有材质被导入或重新生成，就检查它是不是掉到了非本工程的 shader 上。
    ///
    /// 补的是一个时间差，不是覆盖面。<see cref="FI_MaterialShaderTool.ConvertToFiLit"/> 是手动菜单，
    /// 跑完那一刻全工程是对的；但 FBX 的导入模式是 legacy External（materialLocation: 0），
    /// Unity 每次重导入都会按贴图名重新解析材质绑定，找不到同名材质就用**当前管线的默认 shader**
    /// 现铸一个。菜单保证的是一个快照，而导入器随时可以在快照之后改写它。
    ///
    /// 触发条件比想象中日常：<c>BuildingModelPostprocessor.GetVersion()</c> 每 +1 一次，
    /// Assets/Res 下 36 个模型就整体重导一遍。
    ///
    /// 而这种退化在 URP 下**不显眼**——重铸出来的是 Universal Render Pipeline/Lit，画面是一个
    /// 看起来合理的 PBR 效果，只是丢了工程自己的手绘风格，不像 Built-in 时代那样糊一片品红。
    /// 没人会注意到，正是 dock 当初的经过。
    ///
    /// 同步做、且只看本次导入进来的资产，不用 <c>EditorApplication.delayCall</c> 兜到本轮之后。
    /// 实测 delayCall 在没有焦点的编辑器里根本不触发（MCP 驱动、批处理、CI 都是这种状态），
    /// 而一旦不触发，配套的"已排队"静态标志就永久卡住，后面每一次导入都会被它挡掉——
    /// 一个防静默失败的东西自己静默失败了。这里读的只是材质的 shader 引用，没有加载 Prefab
    /// 或配表那类导入期不安全的操作，所以不需要延迟。
    /// </summary>
    public sealed class ResMaterialShaderWatcher : AssetPostprocessor
    {
        private const string ResRoot = "Assets/Res/";

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            List<string> bad = null;

            foreach (string path in importedAssets)
            {
                if (!path.StartsWith(ResRoot, StringComparison.Ordinal)
                    || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || FI_MaterialShaderTool.IsAuthoredShader(material.shader))
                {
                    continue;
                }

                if (bad == null)
                {
                    bad = new List<string>();
                }

                bad.Add($"{material.shader.name} | {path}");
            }

            if (bad == null)
            {
                return;
            }

            Debug.LogError(
                $"[模型导入] Assets/Res 下有 {bad.Count} 个材质不在本工程的 shader 上，" +
                "会渲染成通用 PBR 而不是工程的手绘风格。" +
                "跑 Tools/FI/Convert Item Materials To FI_Lit 修复：\n  " +
                string.Join("\n  ", bad));
        }
    }
}
