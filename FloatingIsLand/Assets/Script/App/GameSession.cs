using System;
using System.Collections.Generic;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.Domain.Wind;

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
        private StageDef _stage;
        private int _stageCount;
        private WindSystem _wind;
        private WindScoreKeeper _windKeeper;
        private int _selectedHandIndex = -1;
        private bool _ended;

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

        /// <summary>风系统（流线渲染 / 调试读它的 <see cref="WindSystem.Field"/>）；地图尚未注入时为 null。</summary>
        public WindSystem Wind
        {
            get { return _wind; }
        }

        /// <summary>地图与建造链路是否已就绪。</summary>
        public bool IsBuildReady
        {
            get { return _board != null; }
        }

        /// <summary>
        /// 调试开关：解锁下一组不检查分数门槛。正式流程是 false（§4.1 的分数门槛解锁），
        /// 只在编辑器里想快速跑穿关卡时才打开。
        /// </summary>
        public bool DebugFreeUnlock { get; set; }

        /// <summary>本关配置；地图尚未注入时为 null。</summary>
        public StageDef Stage
        {
            get { return _stage; }
        }

        /// <summary>一共几关（Stage 表行数）。</summary>
        public int StageCount
        {
            get { return _stageCount; }
        }

        /// <summary>本关是否已达通关分（达标后可进下一关，但不强制）。</summary>
        public bool IsStageCleared
        {
            get { return _run != null && _run.IsStageCleared; }
        }

        /// <summary>本关是不是最后一关。</summary>
        public bool IsFinalStage
        {
            get { return _stage != null && _stage.StageId >= _stageCount; }
        }

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

        /// <summary>
        /// 全局风变更事件：放风帆/物流点导致风场重算后广播（新场从 <see cref="Wind"/> 读）。
        /// 表现层据此重建流线。
        /// </summary>
        public event Action WindFieldChanged;

        /// <summary>风变更结算出的一次性得分（物流风覆盖 / 物流点互联）。表现层据此打辉光、飘字、画互联特效。</summary>
        public event Action<IReadOnlyList<WindAward>> WindAwardsGranted;

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

            // 风系统：从地图的 windSource 元素按局种子展开风源，初始场算好后挂到建造盘。
            // WindSystem 自身实现 IWindField 并转发到当前场，重算不会留下过期引用。
            _wind = new WindSystem(_board, Context != null ? Context.Seed : 0);
            _board.WindField = _wind;
            _windKeeper = new WindScoreKeeper();

            int stageId = Context != null ? Context.StageId : 1;
            _stage = BuildRuleSetFactory.CreateStage(stageId);
            if (_stage == null)
            {
                throw new InvalidOperationException($"Stage 表里没有第 {stageId} 关（关数超出配表？）。");
            }
            _stageCount = BuildRuleSetFactory.StageCount();

            List<LevelDef> levels = BuildRuleSetFactory.CreateLevels();
            List<GroupThemeDef> themes = BuildRuleSetFactory.CreateGroupThemes();
            _run = new BuildRunState(
                levels, themes, _stage,
                Context != null ? Context.Seed : 0,
                Context != null ? Context.CarryScore : 0);
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
            PlacedBuilding placed = _board.Place(blueprint, x, z, layer, rotation, breakdown.Total);

            int consumed = _selectedHandIndex;
            _run.AddBuildScore(breakdown.Total);
            _run.ConsumeFromHand(consumed);

            SettleWindAfterPlacement(blueprint, placed, breakdown);

            // 一次点击只造一栋：落地后退出摆放模式，不自动顺延到下一张手牌。
            // 自动顺延会让玩家在没察觉的情况下把下一栋也甩出去——尤其是手牌前移后
            // 同一个下标已经换成了另一种建筑，鼠标还停在原地就更容易误建。
            _selectedHandIndex = -1;
            SelectionChanged?.Invoke();
            CheckStageEnd();
            return true;
        }

        /// <summary>
        /// 落地后的风结算（用户定稿的一次性语义）：
        /// ① 这栋建筑建造分里若已吃到「接入物流」覆盖分，登记进账本，风变更时不再重复发；
        /// ② 若这栋会改变风（风帆转向 / 物流点延长+物流风），全量重算并广播全局风变更事件，
        ///    再结算风变更新吹出的一次性收益（物流风覆盖 / 物流点互联）计入总分。
        /// 已得的分从不回收——风路之后再变，只发新分。
        /// </summary>
        private void SettleWindAfterPlacement(BuildingBlueprint blueprint, PlacedBuilding placed, ScoreBreakdown breakdown)
        {
            if (_wind == null)
            {
                return;
            }

            if (breakdown.LogisticsCovered)
            {
                _windKeeper.MarkCoveredAtBuild(placed.Id);
            }

            if (!WindSystem.AffectsWind(blueprint))
            {
                return;
            }

            _wind.Recompute();
            List<WindAward> awards = _windKeeper.SettleAfterWindChange(_board, _wind.Field);
            int total = WindScoreKeeper.TotalOf(awards);
            if (total != 0)
            {
                _run.AddBuildScore(total);
            }

            WindFieldChanged?.Invoke();
            if (awards.Count > 0)
            {
                WindAwardsGranted?.Invoke(awards);
            }
        }

        /// <summary>
        /// 干跑：当前选中的建筑摆在这里，风场会变成什么样（风帆/物流点的摆放预览用）。
        /// 选中的不是会改变风的建筑时返回 null。
        /// </summary>
        public WindField PreviewSelectedWind(int x, int z, int layer, Rotation rotation)
        {
            BuildingBlueprint blueprint = SelectedBlueprint;
            if (blueprint == null || _wind == null)
            {
                return null;
            }
            return _wind.Preview(blueprint, x, z, layer, rotation);
        }

        /// <summary>
        /// 干跑：摆在这里会触发哪些风变更一次性分（物流风覆盖 / 物流点互联），
        /// 表现层据此在预览阶段就把「摆下去会给谁发分」飘到受益建筑头上（§7.4 预估得分）。
        /// 不写账本——预览多少次都不消耗一生一次的额度。<paramref name="previewField"/>
        /// 传 <see cref="PreviewSelectedWind"/> 的结果；null（不改风的建筑）时返回 null。
        /// </summary>
        public IReadOnlyList<WindAward> PreviewSelectedWindAwards(
            WindField previewField, int x, int z, int layer, Rotation rotation)
        {
            BuildingBlueprint blueprint = SelectedBlueprint;
            if (blueprint == null || previewField == null || _windKeeper == null)
            {
                return null;
            }

            // 正在摆的假设物流点也要参与互联配对（Id=-1），否则「新点连老点」在预览里看不见
            PlacedBuilding virtualPoint = null;
            if (string.Equals(blueprint.BuildingId, BuildRuleSet.LogisticsPointBuildingId, StringComparison.Ordinal))
            {
                var cells = new List<CellCoord>(blueprint.Footprint.CellCount);
                blueprint.Footprint.GetCells(x, z, rotation, cells);
                virtualPoint = new PlacedBuilding(-1, blueprint, x, z, layer, rotation, cells, 0);
            }

            return _windKeeper.PreviewWindChange(_board, previewField, virtualPoint);
        }

        /// <summary>当前是否处在「二选一」阶段。</summary>
        public bool HasPendingOffers
        {
            get { return _run != null && _run.Offers.Count > 0; }
        }

        /// <summary>选定其中一组建筑，组内全部进手牌。</summary>
        public bool ChooseOffer(int index)
        {
            if (_run == null || !_run.ChooseOffer(index))
            {
                return false;
            }
            // 整组都摆不下（地图快满了）也是结束条件，选完就得判一次
            CheckStageEnd();
            return true;
        }

        /// <summary>
        /// 请求下一组建筑（左下角计分区那个按钮）。达到分数门槛即解锁，**不扣分**。
        /// </summary>
        /// <returns>false = 本关的组已发完，或累计分还没到下一组门槛。</returns>
        public bool RequestNextGroup()
        {
            if (_run == null)
            {
                return false;
            }
            return DebugFreeUnlock ? _run.AdvanceToNextLevel() : _run.TryUnlockNextLevel();
        }

        /// <summary>
        /// 本关是否已经走不下去（GAME_DESIGN §2.2 结束条件之二与之三）：
        /// ① 手牌空、没有待选组，且累计分不够解锁下一组；
        /// ② 手牌还在，但里面没有任何一张在地图上还找得到合法落点。
        ///
        /// ② 是全图逐格试摆，只在「手牌见底 / 刚放完一栋」这种低频时机调用。
        /// </summary>
        public bool IsStuck()
        {
            if (_run == null || _board == null)
            {
                return false;
            }

            if (_run.Hand.Count == 0)
            {
                return _run.Offers.Count == 0 && !CanUnlockNextGroup();
            }

            for (int i = 0; i < _run.Hand.Count; i++)
            {
                BuildingBlueprint blueprint = _rules.GetBlueprintOrNull(_run.Hand[i]);
                if (blueprint != null && _board.HasAnyValidPlacement(blueprint))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>下一组解锁得了吗（调试开关打开时恒为「还有组就行」）。</summary>
        public bool CanUnlockNextGroup()
        {
            if (_run == null || _run.IsLastGroup)
            {
                return false;
            }
            return DebugFreeUnlock || _run.CanAffordNextLevel();
        }

        /// <summary>
        /// 结束本关并产出结算结果。走不下去时由 <see cref="CheckStageEnd"/> 自动调用；
        /// 玩家达到通关分后主动点「进入下一关」也走这里（<paramref name="playerRequested"/> = true）。
        /// 重复调用只生效一次。
        /// </summary>
        public void EndRun(bool playerRequested = false)
        {
            if (_ended)
            {
                return;
            }
            _ended = true;

            var result = new RunResult
            {
                StageId = _stage != null ? _stage.StageId : 0,
                StageName = _stage != null ? _stage.NameCn : "",
                StageScore = _run != null ? _run.StageScore : 0,
                TotalScore = _run != null ? _run.TotalScore : 0,
                ClearScore = _run != null ? _run.ClearScore : 0,
                StageCleared = _run != null && _run.IsStageCleared,
                IsFinalStage = IsFinalStage,
                GroupsPlayed = _run != null ? _run.Level : 0,
                GroupTotal = _run != null ? _run.TotalLevels : 0,
                BuildingsPlaced = _board != null ? _board.Buildings.Count : 0,
            };
            result.EndReason = DescribeEndReason(result, playerRequested);
            Ended?.Invoke(result);
        }

        private string DescribeEndReason(RunResult result, bool playerRequested)
        {
            if (_run == null)
            {
                return "地图未装载，本关无有效进度";
            }
            if (playerRequested)
            {
                return result.IsFinalStage ? "打穿最后一关，主动收官" : "已达通关分，主动进入下一关";
            }
            if (_run.Hand.Count > 0)
            {
                return "手牌里的建筑在地图上都找不到落点了";
            }
            if (_run.IsLastGroup)
            {
                return $"本关 {result.GroupTotal} 组建筑已全部发完";
            }
            return $"分数不足以解锁下一组（还差 {Math.Max(0, _run.NextUnlockScore - _run.TotalScore)} 分）";
        }

        /// <summary>
        /// 每次状态变化后检查本关是否该结束。通关与否都走同一个判定——
        /// 已通关的玩家不是「立刻被赶去下一关」，而是**同样打到走不下去**才结算，
        /// 这样「不强制进入下一关、可以继续摆」才是真的（用户要求）。
        /// </summary>
        private void CheckStageEnd()
        {
            if (_ended || _run == null || _board == null)
            {
                return;
            }
            if (IsStuck())
            {
                EndRun();
            }
        }

        private void OnRunChanged()
        {
            RunChanged?.Invoke();
        }
    }
}
