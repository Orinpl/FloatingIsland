using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Wind;
using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 把「这次摆放的分是谁给的」画出来：给出分的对象叠一层辉光，并在它头顶飘一个 +N / -N。
    ///
    /// 数据全部来自 <see cref="ScoreBreakdown.Attributions"/>——领域层算分时顺手记下的逐实例归因，
    /// 表现层不重算任何规则。这一点很重要：只要预览里飘的是 +3，落地时进账的就一定是 +3，
    /// 两边永远不会分叉。
    ///
    /// 注意分数只结算给**正在摆的那一栋**：被高亮的邻居不会因此涨分，它们的分在自己落地那刻就定死了。
    /// 数字标在邻居头上表达的是"它给你贡献了多少"，不是"它得了多少"。
    /// </summary>
    [RequireComponent(typeof(HighlightGlowRenderer), typeof(FloatingScoreLabels))]
    public sealed class ScoreHighlightPresenter : MonoBehaviour
    {
        /// <summary>落地后余晖保留时长（秒）。让玩家看清"这一下是靠谁拿的分"。</summary>
        private const float PlacedHoldSeconds = 1.4f;

        private static readonly Color BonusLabelColor = new Color(1f, 0.88f, 0.45f, 1f);
        private static readonly Color PenaltyLabelColor = new Color(0.85f, 0.58f, 1f, 1f);

        /// <summary>物流风收益的飘字色（与流线的物流色同一色相，玩家能对上号）。</summary>
        private static readonly Color WindAwardLabelColor = new Color(0.45f, 1f, 0.75f, 1f);

        private HighlightGlowRenderer _glow;
        private FloatingScoreLabels _labels;
        private WorldInstanceIndex _instances;

        /// <summary>落地余晖的到期时刻；0 = 没有余晖。</summary>
        private float _holdUntil;

        // 同一个对象可能同时出现在加分与扣分条目里（配表允许），必须先合成一个净值再飘字，
        // 否则会在同一个位置叠两个数字，糊成一团
        private readonly Dictionary<long, int> _netScore = new Dictionary<long, int>();
        private readonly List<long> _order = new List<long>();

        private void Awake()
        {
            _glow = GetComponent<HighlightGlowRenderer>();
            _labels = GetComponent<FloatingScoreLabels>();
        }

        /// <summary>绑定世界实例索引。由 MapBootstrap 在建造链路就绪后调用。</summary>
        public void Bind(WorldRenderer world)
        {
            _instances = world != null ? world.Instances : null;
            ClearNow();
        }

        /// <summary>刷新摆放预览的高亮。传 null 等同于清空。</summary>
        public void ShowPreview(ScoreBreakdown breakdown)
        {
            // 预览优先于落地余晖：玩家已经拿起下一栋了，还挂着上一次的光只会干扰
            _holdUntil = 0f;
            Apply(breakdown);
        }

        /// <summary>落地成功后放一次余晖：同一批高亮多留一会儿再自己消失。</summary>
        public void PlayPlaced(ScoreBreakdown breakdown)
        {
            Apply(breakdown);
            _holdUntil = Time.time + PlacedHoldSeconds;
        }

        /// <summary>
        /// 把风变更结算出的一次性收益叠加到当前余晖上（在 <see cref="PlayPlaced"/> 之后调用）：
        /// 被物流风覆盖的建筑亮金光飘分；互联的两个物流点各亮一次，分数飘在下游那个头上。
        /// 刻意不 Begin/Clear——这些收益和落地余晖是同一次点击的产物，要一起看。
        /// </summary>
        public void PlayWindAwards(IReadOnlyList<WindAward> awards)
        {
            if (_glow == null || _labels == null || _instances == null || awards == null || awards.Count == 0)
            {
                return;
            }

            for (int i = 0; i < awards.Count; i++)
            {
                WindAward award = awards[i];
                WorldInstanceIndex.Entry entry;
                if (award.Kind == WindAwardKind.LogisticsCoverage)
                {
                    if (_instances.TryGetBuilding(award.BuildingInstanceId, out entry))
                    {
                        _glow.Add(entry, HighlightGlowRenderer.GlowStyle.Bonus);
                        _labels.Add(entry.LabelAnchor, $"+{award.Score} 接入物流", WindAwardLabelColor);
                    }
                    continue;
                }

                // 互联：两端都发光，数字标在下游（新连上的那个）
                if (_instances.TryGetBuilding(award.BuildingInstanceId, out entry))
                {
                    _glow.Add(entry, HighlightGlowRenderer.GlowStyle.Bonus);
                }
                if (_instances.TryGetBuilding(award.OtherInstanceId, out entry))
                {
                    _glow.Add(entry, HighlightGlowRenderer.GlowStyle.Bonus);
                    _labels.Add(entry.LabelAnchor, $"+{award.Score} 物流互联", WindAwardLabelColor);
                }
            }

            _holdUntil = Time.time + PlacedHoldSeconds + 0.8f;
        }

        /// <summary>清预览。落地余晖还没放完时不打断它。</summary>
        public void ClearPreview()
        {
            if (_holdUntil > Time.time)
            {
                return;
            }
            ClearNow();
        }

        private void Update()
        {
            if (_holdUntil > 0f && Time.time >= _holdUntil)
            {
                _holdUntil = 0f;
                ClearNow();
            }
        }

        private void ClearNow()
        {
            if (_glow != null)
            {
                _glow.Clear();
            }
            if (_labels != null)
            {
                _labels.Clear();
            }
        }

        private void Apply(ScoreBreakdown breakdown)
        {
            if (_glow == null || _labels == null)
            {
                return;
            }

            _glow.Begin();
            _labels.Begin();

            if (breakdown != null && _instances != null)
            {
                _netScore.Clear();
                _order.Clear();

                IReadOnlyList<ScoreAttribution> attributions = breakdown.Attributions;
                for (int i = 0; i < attributions.Count; i++)
                {
                    ScoreAttribution attribution = attributions[i];
                    if (attribution.Kind != ScoreSourceKind.Building && attribution.Kind != ScoreSourceKind.Element)
                    {
                        // 基础分与风力没有第二个对象可指，只出现在明细里
                        continue;
                    }

                    long key = MakeKey(attribution.Kind, attribution.InstanceId);
                    int accumulated;
                    if (!_netScore.TryGetValue(key, out accumulated))
                    {
                        _order.Add(key);
                    }
                    _netScore[key] = accumulated + attribution.Score;
                }

                for (int i = 0; i < _order.Count; i++)
                {
                    long key = _order[i];
                    Emit(key, _netScore[key]);
                }
            }

            _labels.End();
        }

        private void Emit(long key, int score)
        {
            ScoreSourceKind kind = (ScoreSourceKind)(int)(key >> 32);
            int instanceId = (int)(key & 0xFFFFFFFFL);

            WorldInstanceIndex.Entry entry;
            bool found = kind == ScoreSourceKind.Building
                ? _instances.TryGetBuilding(instanceId, out entry)
                : _instances.TryGetElement(instanceId, out entry);
            if (!found)
            {
                return;
            }

            if (score == 0)
            {
                // 在范围内但没算上（超上限 / 不叠加 / 收益归零）。给一层很淡的光但不飘数字——
                // 飘个 "+0" 只会让玩家以为算错了
                _glow.Add(entry, HighlightGlowRenderer.GlowStyle.Ignored);
                return;
            }

            bool bonus = score > 0;
            _glow.Add(entry, bonus
                ? HighlightGlowRenderer.GlowStyle.Bonus
                : HighlightGlowRenderer.GlowStyle.Penalty);
            _labels.Add(
                entry.LabelAnchor,
                bonus ? $"+{score}" : score.ToString(),
                bonus ? BonusLabelColor : PenaltyLabelColor);
        }

        private static long MakeKey(ScoreSourceKind kind, int instanceId)
        {
            return ((long)(int)kind << 32) | (uint)instanceId;
        }
    }
}
