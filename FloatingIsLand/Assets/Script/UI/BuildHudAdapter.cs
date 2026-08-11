using System.Collections.Generic;
using FloatingIsLand.App;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.GameInput;
using UnityEngine;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 局内建造 HUD 的桥接：<see cref="GameSession"/> ↔ <see cref="HudPanel"/>。
    ///
    /// 与 <see cref="FlowUIAdapter"/> 分工明确：那个管局外流程（开始/下一关/回主菜单），
    /// 这个只管局内建造（计分区、手牌、建筑组二选一）。两者都遵守同一条铁律——
    /// 面板是哑视图，App 层不认识任何 UI 类型。
    ///
    /// 会话是每局新建的，所以这里每帧检查 GameFlow.CurrentSession 是否换了对象再重新订阅，
    /// 而不是只在 Start 里绑一次。
    /// </summary>
    [RequireComponent(typeof(UIManager))]
    public sealed class BuildHudAdapter : MonoBehaviour
    {
        private UIManager _ui;
        private HudPanel _hud;
        private GameSession _session;
        private readonly List<HudPanel.HandItemView> _handItems = new List<HudPanel.HandItemView>();
        private readonly List<string> _offerLabels = new List<string>();

        private void Start()
        {
            _ui = GetComponent<UIManager>();
            _hud = _ui.Get<HudPanel>();
            _hud.HandItemClicked += OnHandItemClicked;
            _hud.OfferClicked += OnOfferClicked;

            // 触屏工具条：面板只管长相，按下去干什么由这里翻译成玩法指令。
            // 摆放交互在 Game.View，这个程序集够不着，所以走 App 层的 BuildCommandBus 转一手。
            _hud.RotateClicked += BuildCommandBus.RequestRotate;
            _hud.ConfirmClicked += BuildCommandBus.RequestPlace;
            _hud.CancelClicked += BuildCommandBus.RequestCancel;

            if (_hud.nextGroupButton != null)
            {
                _hud.nextGroupButton.onClick.AddListener(OnNextGroupClicked);
            }
        }

        private void OnDestroy()
        {
            if (_hud != null)
            {
                _hud.HandItemClicked -= OnHandItemClicked;
                _hud.OfferClicked -= OnOfferClicked;
                _hud.RotateClicked -= BuildCommandBus.RequestRotate;
                _hud.ConfirmClicked -= BuildCommandBus.RequestPlace;
                _hud.CancelClicked -= BuildCommandBus.RequestCancel;
                if (_hud.nextGroupButton != null)
                {
                    _hud.nextGroupButton.onClick.RemoveListener(OnNextGroupClicked);
                }
            }
            Unsubscribe();
        }

        private void Update()
        {
            GameFlow flow = GameFlow.Instance;
            GameSession current = flow != null ? flow.CurrentSession : null;
            if (!ReferenceEquals(current, _session))
            {
                Unsubscribe();
                _session = current;
                Subscribe();
                Refresh();
            }

            UpdatePlacingHud();
        }

        /// <summary>
        /// 摆放态下每帧要变的两样东西：触屏工具条的显隐/可用，以及「预计得分 N / 摆不下的原因」。
        /// 这两样都跟着光标（手指）走，事件驱动的 <see cref="Refresh"/> 追不上，只能每帧刷。
        /// 文本先比对再赋值——每帧写 Text.text 会让 Canvas 每帧重建一次网格。
        /// </summary>
        private void UpdatePlacingHud()
        {
            if (_hud == null)
            {
                return;
            }

            bool placing = _session != null && _session.IsBuildReady && _session.SelectedBlueprint != null;
            _hud.SetTouchControls(placing && PointerInput.IsTouchMode, BuildPreviewState.CanPlace, "建造");

            if (!placing)
            {
                // 非摆放态的提示是事件驱动的（选组 / 手牌空 / 通关达标），别在这儿每帧盖掉
                _liveHint = null;
                return;
            }

            string preview = BuildPreviewState.Message;
            string hint = string.IsNullOrEmpty(preview)
                ? PlacementHint()
                : preview + "\n" + PlacementHint();
            if (!string.Equals(hint, _liveHint, System.StringComparison.Ordinal))
            {
                _liveHint = hint;
                _hud.SetHint(hint);
            }
        }

        /// <summary>上一帧写进提示栏的文本，用来避开每帧重建 Canvas。</summary>
        private string _liveHint;

        private void Subscribe()
        {
            if (_session == null)
            {
                return;
            }
            _session.RunChanged += Refresh;
            _session.SelectionChanged += Refresh;
            _session.BuildReady += Refresh;
        }

        private void Unsubscribe()
        {
            if (_session == null)
            {
                return;
            }
            _session.RunChanged -= Refresh;
            _session.SelectionChanged -= Refresh;
            _session.BuildReady -= Refresh;
        }

        private void OnHandItemClicked(int index)
        {
            if (_session == null)
            {
                return;
            }
            // 再点一次已选中的那张 = 取消选中，退出摆放模式
            _session.SelectHandItem(_session.SelectedHandIndex == index ? -1 : index);
        }

        private void OnOfferClicked(int index)
        {
            if (_session != null)
            {
                _session.ChooseOffer(index);
            }
        }

        private void OnNextGroupClicked()
        {
            if (_session == null)
            {
                return;
            }

            // 还没选组就先把当前这批选掉（默认取第一组），否则连点会跳过整组建筑
            if (_session.HasPendingOffers)
            {
                _session.ChooseOffer(0);
                return;
            }

            if (!_session.RequestNextGroup())
            {
                BuildRunState run = _session.Run;
                _hud.SetHint(run != null && run.IsLastGroup
                    ? "本关的建筑组已经全部发完了。"
                    : $"分数不够——下一组需要 {(run != null ? run.NextUnlockScore : 0)} 分。");
            }
        }

        private void Refresh()
        {
            if (_hud == null)
            {
                return;
            }

            if (_session == null || !_session.IsBuildReady)
            {
                _hud.SetScoreboard(0, 0, false, 0, 0, "等待地图装载", false);
                _hud.SetHand(null, -1);
                _hud.SetOffers(null);
                _hud.SetHint(string.Empty);
                return;
            }

            BuildRunState run = _session.Run;

            _handItems.Clear();
            for (int i = 0; i < run.Hand.Count; i++)
            {
                string variantId = run.Hand[i];
                BuildingBlueprint blueprint = _session.Rules.GetBlueprintOrNull(variantId);
                _handItems.Add(new HudPanel.HandItemView(
                    variantId,
                    blueprint != null ? blueprint.NameCn : variantId,
                    blueprint != null ? blueprint.Footprint : null,
                    blueprint != null ? blueprint.PrefabPath : null));
            }

            _offerLabels.Clear();
            for (int g = 0; g < run.Offers.Count; g++)
            {
                _offerLabels.Add(DescribeGroup(run.Offers[g]));
            }

            bool lastGroup = run.IsLastGroup;
            string buttonLabel;
            if (_session.HasPendingOffers)
            {
                buttonLabel = "选第一组";
            }
            else if (lastGroup)
            {
                buttonLabel = "本关建筑组已发完";
            }
            else if (_session.DebugFreeUnlock)
            {
                buttonLabel = "解锁下一组（调试免费）";
            }
            else if (run.CanAffordNextLevel())
            {
                buttonLabel = $"解锁下一组（{run.NextUnlockScore} 分达标）";
            }
            else
            {
                // 门槛是准入不是消耗，所以这里显示「还差多少」而不是「花多少」
                buttonLabel = $"下一组需 {run.NextUnlockScore} 分（还差 {run.NextUnlockScore - run.TotalScore}）";
            }

            bool interactable = _session.HasPendingOffers || _session.CanUnlockNextGroup();

            _hud.SetScoreboard(
                run.TotalScore, run.ClearScore, run.IsStageCleared,
                run.Level, run.TotalLevels, buttonLabel, interactable);
            _hud.SetEndRunButton(EndRunLabel(run));
            _hud.SetHand(_handItems, _session.SelectedHandIndex);
            _hud.SetOffers(_offerLabels);
            _hud.SetHint(BuildHint(run));
        }

        /// <summary>
        /// 达通关分后这个按钮才是「进入下一关」；没达标时点它等于提前认输，
        /// 所以标签要把后果说清楚，不能都叫「结束本局」。
        /// </summary>
        private string EndRunLabel(BuildRunState run)
        {
            if (!run.IsStageCleared)
            {
                return "提前结束（不再继续）";
            }
            return _session.IsFinalStage ? "通关收官 · 上榜" : "进入下一关";
        }

        private string BuildHint(BuildRunState run)
        {
            if (_session.HasPendingOffers)
            {
                return "选择一组建筑（点中间的组按钮）。";
            }
            if (run.IsStageCleared && run.Hand.Count == 0)
            {
                return _session.IsFinalStage
                    ? "已达通关分——可以继续刷分，或点右上角收官上榜。"
                    : "已达通关分——可以继续刷分攒下一关的底子，或点右上角进入下一关。";
            }
            if (run.Hand.Count == 0)
            {
                return $"手牌已空——攒够 {run.NextUnlockScore} 分即可解锁下一组。";
            }
            if (_session.SelectedBlueprint == null)
            {
                return "点下方建筑进入摆放模式。";
            }
            return PlacementHint();
        }

        /// <summary>
        /// 摆放操作提示，分平台给：PC 有滚轮和 Esc，手机没有，得换成「再点一次」和工具条按钮。
        /// 提示文案照抄错平台是新手最容易卡住的地方——手机上写着「滚轮旋转」等于没写。
        /// </summary>
        private string PlacementHint()
        {
            // 风帆的滚轮/旋转键不是旋转模型，而是切换风的左/右转向——预览流线会跟着变
            bool sail = string.Equals(
                _session.SelectedBlueprint.BuildingId, BuildRuleSet.SailBuildingId, System.StringComparison.Ordinal);

            if (PointerInput.IsTouchMode)
            {
                return sail
                    ? "风帆须建在风带上；点地面选落点，「旋转」切换左/右转向（看流线预览），再点同一格或按「建造」放下。"
                    : "点地面选落点，再点同一格（或按「建造」）放下；「旋转」转 90°，「取消」退出。";
            }
            return sail
                ? "风帆须建在风带上；滚轮切换左/右转向（看流线预览），左键放置，Esc 取消。"
                : "滚轮旋转，左键放置（放完退出摆放模式），Esc 取消。";
        }

        private string DescribeGroup(BuildingGroup group)
        {
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            for (int i = 0; i < group.VariantIds.Count; i++)
            {
                BuildingBlueprint blueprint = _session.Rules.GetBlueprintOrNull(group.VariantIds[i]);
                string name = blueprint != null ? blueprint.NameCn : group.VariantIds[i];
                if (!counts.ContainsKey(name))
                {
                    counts[name] = 0;
                    order.Add(name);
                }
                counts[name]++;
            }

            var parts = new List<string>(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                string name = order[i];
                parts.Add(counts[name] > 1 ? $"{name}×{counts[name]}" : name);
            }

            string detail = string.Join("  ", parts);
            // 主题名放最前：二选一的意义是「选一条路线」，光看建筑清单玩家得自己去脑补这组想干什么
            return string.IsNullOrEmpty(group.NameCn) ? detail : $"【{group.NameCn}】{detail}";
        }
    }
}
