using System;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// Assets/Res/&lt;资产名&gt;/fbx/&lt;资产名&gt;.fbx 的导入设置：关掉这批资产用不到的子资产，省导入时间。
    ///
    /// **这里不做任何对位。** 按 footprint 缩放、轴心归位都在 <see cref="ModelPrefabGenerator"/> 里做，
    /// 原因有两条，都踩过：
    ///   1. 这批 FBX 是 Blender 导出的 Z-up，导入器把 -90°X 的轴向修正烤在模型根 Transform 上，
    ///      导入后处理器看到的根局部空间还是 Z-up——在那里量包围盒，「宽 × 深」量出来的其实是「宽 × 高」，
    ///      按格缩放从一开始就对不准。
    ///   2. 轴心归位要把网格相对根节点挪一段，可这批模型是单节点 FBX，网格就挂在根上、根没法相对自己偏移，
    ///      遍历子节点的写法是空转。必须在外面包一层才有地方放偏移。
    /// 两件事都要求「模型已经在一层 identity 的壳里」，那是 Prefab 生成阶段才有的条件。
    /// </summary>
    public sealed class BuildingModelPostprocessor : AssetPostprocessor
    {
        private const string ResRoot = "Assets/Res/";

        /// <summary>
        /// 改了导入规则后把这个数 +1：Unity 用它判断后处理器是否变更，
        /// 变更时自动重新导入所有受影响的模型（否则已导入的资产不会重跑）。
        ///
        /// 3 → 4：移除了按 footprint 的缩放与轴心归位，重导一次让 v3 烤进导入资产的缩放归零、
        /// FBX 根恢复成单位换算的 100，便于人工核对。
        /// 注意这不是顺序敏感的硬约束：<c>AlignToFootprint</c> 是绝对拟合（缩放系数 = 目标尺寸 / 当前实测尺寸），
        /// 进来时残留多少倍都会被算掉，不会二次缩放。
        /// </summary>
        public override uint GetVersion()
        {
            return 5;
        }

        private void OnPreprocessModel()
        {
            if (!IsResModel(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            // 生成的建筑资产没有动画/相机/灯光，关掉省导入时间与无用子资产
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            // 打开 Read/Write：占格探针要逐三角形投影到 XZ 平面量模型实际盖住哪些格，
            // 关着的话连编辑器里都读不到顶点（Unity 会报 "Not allowed to access vertices"），
            // 而探针读不到就只能给出"没量到"——比不量更危险。
            // 代价是网格数据在内存里多留一份；这批是 22 个低模，可以忽略。
            importer.isReadable = true;
        }

        /// <summary>
        /// FBX 重导后自动跑一次对位校验。
        ///
        /// 对齐结果是烤在 Prefab 的 transform override 里的，重导只会换掉网格、不会重算缩放和轴心。
        /// 也就是说「美术换了模型」之后，模型变了、尺寸没变，而且编辑器、运行时、Console 三处都不会吭声。
        /// 改造前对齐挂在导入后处理里、这条路径是自愈的，所以必须补一个信号把静默退化变成一条可见的报错。
        /// 这里只做校验不自动重生成：在导入回调里批量改 Prefab 资产太容易和 AssetDatabase 的批处理打架。
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (_validationQueued)
            {
                return;
            }

            foreach (string path in importedAssets)
            {
                if (!IsResModel(path))
                {
                    continue;
                }

                _validationQueued = true;
                // 延到本轮导入结束再跑：校验要加载 Prefab 内容与配表，导入回调里做不安全
                EditorApplication.delayCall += RunQueuedValidation;
                return;
            }
        }

        private static bool _validationQueued;

        private static void RunQueuedValidation()
        {
            _validationQueued = false;
            Debug.Log("[模型导入] 检测到 Assets/Res 下的 FBX 重新导入，自动校验一次 Prefab 对位" +
                      "（对齐是烤在 Prefab 里的，重导不会自动更新——有问题就跑 Tools/美术/生成白模 Prefab）。");
            ModelPrefabGenerator.ValidateAlignment();
        }

        private static bool IsResModel(string path)
        {
            return path.StartsWith(ResRoot, StringComparison.Ordinal)
                   && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }
    }
}
