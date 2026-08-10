using UnityEngine;

namespace FloatingIsLand.App
{
    /// <summary>
    /// 一局的局外参数：第几关（局数序号）+ 地图种子。
    /// "下一关" = 换新地图（新种子）再开一局；关数目前无上限、也无每关差异化配表——
    /// 若后续需要不同关卡有不同地图尺寸/目标分，加 Stage 表后在本类扩展字段（见 Docs/BOOT_FRAMEWORK.md）。
    /// 注意与 Level 表区分：Level 表是"局内 20 级进度"（解锁费用/可用建筑组主题），不是这里的"关"。
    /// </summary>
    public sealed class RunContext
    {
        /// <summary>第几关，从 1 开始（UI 显示用）。</summary>
        public int RunIndex { get; }

        /// <summary>本局随机种子：地图生成、建筑组抽取共用同一种子，保证整局可复现（PROJECT_BUILD §1.2）。</summary>
        public int Seed { get; }

        public RunContext(int runIndex, int seed)
        {
            RunIndex = runIndex;
            Seed = seed;
        }

        /// <summary>主界面开新游戏：第 1 关，随机种子。</summary>
        public static RunContext CreateFirst()
        {
            return new RunContext(1, NewSeed());
        }

        /// <summary>结算后进下一关：关数 +1，换新种子（新地图）。</summary>
        public RunContext CreateNext()
        {
            return new RunContext(RunIndex + 1, NewSeed());
        }

        private static int NewSeed()
        {
            return Random.Range(int.MinValue, int.MaxValue);
        }
    }
}
