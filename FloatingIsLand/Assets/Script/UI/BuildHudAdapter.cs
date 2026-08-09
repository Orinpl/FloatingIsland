using System.Collections.Generic;
using FloatingIsLand.App;
using FloatingIsLand.Domain.Build;
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
        private readonly List<string> _handLabels = new List<string>();
        private readonly List<string> _offerLabels = new List<string>();

        private void Start()
        {
            _ui = GetComponent<UIManager>();
            _hud = _ui.Get<HudPanel>();
            _hud.HandItemClicked += OnHandItemClicked;
            _hud.OfferClicked += OnOfferClicked;

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
        }

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

            // 还没选组就先把当前这批选掉（demo 里默认取第一组），否则连点会跳过整级建筑
            if (_session.HasPendingOffers)
            {
                _session.ChooseOffer(0);
                return;
            }

            if (!_session.RequestNextGroup())
            {
                _hud.SetHint("已经是最后一级，没有更多建筑组了。");
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
                _hud.SetScoreboard(0, 0, 0, 0, "等待地图装载", false);
                _hud.SetHand(null, -1);
                _hud.SetOffers(null);
                _hud.SetHint(string.Empty);
                return;
            }

            BuildRunState run = _session.Run;

            _handLabels.Clear();
            for (int i = 0; i < run.Hand.Count; i++)
            {
                BuildingBlueprint blueprint = _session.Rules.GetBlueprintOrNull(run.Hand[i]);
                _handLabels.Add(blueprint != null ? blueprint.NameCn : run.Hand[i]);
            }

            _offerLabels.Clear();
            for (int g = 0; g < run.Offers.Count; g++)
            {
                _offerLabels.Add(DescribeGroup(run.Offers[g]));
            }

            bool lastLevel = run.Level >= run.TotalLevels;
            string buttonLabel;
            if (_session.HasPendingOffers)
            {
                buttonLabel = "选第一组";
            }
            else if (lastLevel)
            {
                buttonLabel = "已达最高级";
            }
            else if (_session.DemoFreeUnlock)
            {
                buttonLabel = "解锁下一组（demo 免费）";
            }
            else
            {
                buttonLabel = $"解锁下一组（{run.NextUnlockCost} 金币）";
            }

            bool interactable = _session.HasPendingOffers
                                || (!lastLevel && (_session.DemoFreeUnlock || run.CanAffordNextLevel()));

            _hud.SetScoreboard(run.TotalScore, run.Gold, run.Level, run.TotalLevels, buttonLabel, interactable);
            _hud.SetHand(_handLabels, _session.SelectedHandIndex);
            _hud.SetOffers(_offerLabels);
            _hud.SetHint(BuildHint(run));
        }

        private string BuildHint(BuildRunState run)
        {
            if (_session.HasPendingOffers)
            {
                return "选择一组建筑（点中间的组按钮）。";
            }
            if (run.Hand.Count == 0)
            {
                return "手牌已空——点左下角「解锁下一组」继续。";
            }
            if (_session.SelectedBlueprint == null)
            {
                return "点下方建筑进入摆放模式。";
            }
            return "滚轮旋转，左键放置，Esc 取消。";
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
            return string.Join("  ", parts);
        }
    }
}
