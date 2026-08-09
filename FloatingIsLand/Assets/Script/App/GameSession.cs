using System;
using System.Collections.Generic;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.App
{
    /// <summary>
    /// 局内会话：把领域层的建造盘 / 计分引擎 / 进度状态组装成一条可驱动的核心循环，
    /// 并作为 UI 与表现层的唯一入口（两边都只引用 Game.App，互不认识）。
    ///
    /// 循环（PROJECT_BUILD §4.7）：抽组 → 选组 → 逐栋摆放 → 结算发钱 → 解锁下一级。
    ///
    /// 地图不在这里读：Main 场景的 MapBootstrap 装载完快照后调 <see cref="AttachMap"/> 注入，
    /// 会话本身不碰 Unity 资源，保持可单测。
    /// </summary>
    public sealed class GameSession
    {
        private BuildRuleSet _rules;
        private BuildBoard _board;
        private ScoreEngine _scoring;
        private BuildRunState _run;
        private int _selectedHandIndex = -1;

        /// <summary>本局局外参数（关数 + 种子）。</summary>
        public RunContext Context { get; }

        /// <summary>建造盘；地图尚未注入时为 null。</summary>
        public BuildBoard Board
        {
            get { return _board; }
        }

        /// <summary>计分引擎；地图尚未注入时为 null。</summary>
        public ScoreEngine Scoring
        {
            get { return _scoring; }
        }

        /// <summary>局内进度（等级 / 手牌 / 分数 / 金币）；地图尚未注入时为 null。</summary>
        public BuildRunState Run
        {
            get { return _run; }
        }

        /// <summary>建造规则集；地图尚未注入时为 null。</summary>
        public BuildRuleSet Rules
        {
            get { return _rules; }
        }

        /// <summary>地图与建造链路是否已就绪。</summary>
        public bool IsBuildReady
        {
            get { return _board != null; }
        }

        /// <summary>
        /// 演示模式：解锁下一组不检查金币也不扣费（用户要求的临时可玩 demo——
        /// 可以一直建，建完一组直接点得分按钮拿下一组）。正式版关掉即回到 §4.1 的金币解锁。
        /// </summary>
        public bool DemoFreeUnlock { get; set; } = true;

        /// <summary>当前选中的手牌下标；-1 = 未选中。</summary>
        public int SelectedHandIndex
        {
            get { return _selectedHandIndex; }
        }

        /// <summary>当前选中的建筑蓝图；未选中返回 null。</summary>
        public BuildingBlueprint SelectedBlueprint
        {
            get
            {
                if (_run == null || _selectedHandIndex < 0 || _selectedHandIndex >= _run.Hand.Count)
                {
                    return null;
                }
                return _rules.GetBlueprintOrNull(_run.Hand[_selectedHandIndex]);
            }
        }

        /// <summary>建造链路就绪（地图注入完成）。表现层据此开始渲染元素与建筑。</summary>
        public event Action BuildReady;

        /// <summary>选中的手牌变了（含取消选中）。表现层据此开关 ghost。</summary>
        public event Action SelectionChanged;

        /// <summary>局内进度变了（分数 / 金币 / 手牌 / 待选组）。UI 据此刷新。</summary>
        public event Action RunChanged;

        /// <summary>本局结束（终局结算完成）。GameplayState 订阅后驱动流程进入 Settlement。</summary>
        public event Action<RunResult> Ended;

        public GameSession(RunContext context)
        {
            Context = context;
        }

        /// <summary>
        /// 注入本局地图并搭建建造链路。由 MapBootstrap 在地图装载完成后调用一次。
        /// </summary>
        public void AttachMap(MapSnapshot map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            if (_board != null)
            {
                // 一局一张图；重复注入说明场景装载流程被走了两次
                throw new InvalidOperationException("本局地图已注入，不能重复 AttachMap。");
            }

            _rules = BuildRuleSetFactory.Create();
            _board = new BuildBoard(map, _rules);
            _scoring = new ScoreEngine(_board);

            List<LevelDef> levels = BuildRuleSetFactory.CreateLevels();
            _run = new BuildRunState(levels, _rules, Context != null ? Context.Seed : 0);
            _run.Changed += OnRunChanged;
            _run.Start();

            BuildReady?.Invoke();
            RunChanged?.Invoke();
        }

        /// <summary>选中手牌里的一张准备摆放；传 -1 取消选中。</summary>
        public void SelectHandItem(int handIndex)
        {
            if (_run == null)
            {
                return;
            }
            if (handIndex < -1 || handIndex >= _run.Hand.Count)
            {
                return;
            }
            if (_selectedHandIndex == handIndex)
            {
                return;
            }

            _selectedHandIndex = handIndex;
            SelectionChanged?.Invoke();
        }

        /// <summary>取消当前选中。</summary>
        public void ClearSelection()
        {
            SelectHandItem(-1);
        }

        /// <summary>干跑：当前选中的建筑摆在这里合不合法。</summary>
        public PlacementCheck CheckSelectedPlacement(int x, int z, int layer, Rotation rotation)
        {
            BuildingBlueprint blueprint = SelectedBlueprint;
            if (blueprint == null)
            {
                return PlacementCheck.Fail(PlacementFailure.UnknownBlueprint, "没有选中建筑。");
            }
            return _board.CanPlace(blueprint, x, z, layer, rotation);
        }

        /// <summary>
        /// 干跑：当前选中的建筑在这里**逐格**的落点合法性（表现层把落点格逐个标绿/标红用）。
        /// 整体结论仍以 <see cref="CheckSelectedPlacement"/> 为准——矿脉范围、风带这类整体规则
        /// 不属于任何单格，可能出现「每格都合格、整体却摆不下」。
        /// </summary>
        public void CheckSelectedCells(int x, int z, int layer, Rotation rotation, List<CellPlacement> result)
        {
            BuildingBlueprint blueprint = SelectedBlueprint;
            if (blueprint == null)
            {
                result?.Clear();
                return;
            }
            _board.CheckCells(blueprint, x, z, layer, rotation, result);
        }

        /// <summary>干跑：当前选中的建筑摆在这里能得多少分（摆放前预览用，§7.4）。</summary>
        public ScoreBreakdown PreviewSelectedScore(int x, int z, int layer, Rotation rotation)
        {
            BuildingBlueprint blueprint = SelectedBlueprint;
            return blueprint == null ? null : _scoring.Evaluate(blueprint, x, z, layer, rotation);
        }

        /// <summary>
        /// 把当前选中的建筑落到这里：校验 → 落地 → 结算即时分 → 消耗手牌 → 退出摆放模式。
        /// 非法位置返回 false 且不改变任何状态（原因见返回的 check）。
        /// </summary>
        public bool TryPlaceSelected(int x, int z, int layer, Rotation rotation, out PlacementCheck check, out ScoreBreakdown breakdown)
        {
            breakdown = null;
            BuildingBlueprint blueprint = SelectedBlueprint;
            if (blueprint == null)
            {
                check = PlacementCheck.Fail(PlacementFailure.UnknownBlueprint, "没有选中建筑。");
                return false;
            }

            check = _board.CanPlace(blueprint, x, z, layer, rotation);
            if (!check.IsValid)
            {
                return false;
            }

            // 先算分再落地：计分要看的是「落地前的邻居」，把自己算进去会自己给自己加同类分
            breakdown = _scoring.Evaluate(blueprint, x, z, layer, rotation);
            _board.Place(blueprint, x, z, layer, rotation, breakdown.Total);

            int consumed = _selectedHandIndex;
            _run.AddBuildScore(breakdown.Total);
            _run.ConsumeFromHand(consumed);

            // 一次点击只造一栋：落地后退出摆放模式，不自动顺延到下一张手牌。
            // 自动顺延会让玩家在没察觉的情况下把下一栋也甩出去——尤其是手牌前移后
            // 同一个下标已经换成了另一种建筑，鼠标还停在原地就更容易误建。
            _selectedHandIndex = -1;
            SelectionChanged?.Invoke();
            return true;
        }

        /// <summary>当前是否处在「二选一」阶段。</summary>
        public bool HasPendingOffers
        {
            get { return _run != null && _run.Offers.Count > 0; }
        }

        /// <summary>选定其中一组建筑，组内全部进手牌。</summary>
        public bool ChooseOffer(int index)
        {
            return _run != null && _run.ChooseOffer(index);
        }

        /// <summary>
        /// 请求下一组建筑（左下角计分区那个按钮）。
        /// <see cref="DemoFreeUnlock"/> 为 true 时不看金币直接给；否则按 §4.1 扣解锁费用。
        /// </summary>
        /// <returns>false = 已经是最后一级，或金币不足。</returns>
        public bool RequestNextGroup()
        {
            if (_run == null)
            {
                return false;
            }
            return DemoFreeUnlock ? _run.AdvanceToNextLevel() : _run.TryUnlockNextLevel();
        }

        /// <summary>结束本局并产出结算结果。</summary>
        public void EndRun()
        {
            var result = new RunResult
            {
                TotalScore = _run != null ? _run.TotalScore : 0,
                EndReason = _run == null
                    ? "地图未装载，本局无有效进度"
                    : $"手动结束：第 {_run.Level}/{_run.TotalLevels} 级，已建 {(_board != null ? _board.Buildings.Count : 0)} 栋",
            };
            Ended?.Invoke(result);
        }

        private void OnRunChanged()
        {
            RunChanged?.Invoke();
        }
    }
}
