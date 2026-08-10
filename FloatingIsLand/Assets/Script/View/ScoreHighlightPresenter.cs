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

        /// <summary>
        /// 刷新摆放预览的高亮。传 null 等同于清空。
        /// <paramref name="selfAnchor"/> = 被摆建筑自己的飘字锚点（占地中心上方）——
        /// 建筑自己身上要显示「范围内所有加减分的总和」，建在风带上时再逐条显示风的加减分（§7.4）。
        /// <paramref name="windAwards"/> = 摆下去会触发的风变更一次性分（干跑结果，可空）——
        /// 预览阶段就把「会给谁发分」飘到受益建筑头上，落地后看到的数字与预览完全一致。
        /// </summary>
        public void ShowPreview(ScoreBreakdown breakdown, Vector3 selfAnchor, IReadOnlyList<WindAward> windAwards)
        {
            // 预览优先于落地余晖：玩家已经拿起下一栋了，还挂着上一次的光只会干扰
            _holdUntil = 0f;
            Apply(breakdown, selfAnchor, windAwards);
        }

        /// <summary>落地成功后放一次余晖：同一批高亮多留一会儿再自己消失。</summary>
        public void PlayPlaced(ScoreBreakdown breakdown, Vector3 selfAnchor)
        {
            Apply(breakdown, selfAnchor, null);
            _holdUntil = Time.time + PlacedHoldSeconds;
        }

        /// <summary>
        /// 把风变更结算出的一次性收益叠加到当前余晖上（在 <see cref="PlayPlaced"/> 之后调用）：
        /// 被物流风覆盖的建筑亮金光飘分；互联的两个物流点各亮一次，分数飘在下游那个头上。
        /// 刻意不 Begin/Clear——这些收益和落地余晖是同一次点击的产物，要一起看。
        /// </summary>
        public void PlayWindAwards(IReadOnlyList<WindAward> awards)
        {
            if (_glow == null || _labels == null || awards == null || awards.Count == 0)
            {
                return;
            }

            EmitWindAwards(awards);
            _holdUntil = Time.time + PlacedHoldSeconds + 0.8f;
        }

        /// <summary>
        /// 把一批风变更收益画到受益建筑身上（预览批次与落地余晖共用）。
        /// 互联的数字优先标在下游那端；下游是还没落地的假设点（Id=-1，索引里找不到）时
        /// 退回标在上游——预览「新点连老点」时数字不能凭空消失。
        /// </summary>
        private void EmitWindAwards(IReadOnlyList<WindAward> awards)
        {
            if (_instances == null)
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

                WorldInstanceIndex.Entry upstream = null;
                if (_instances.TryGetBuilding(award.BuildingInstanceId, out entry))
                {
                    upstream = entry;
                    _glow.Add(entry, HighlightGlowRenderer.GlowStyle.Bonus);
                }
                if (_instances.TryGetBuilding(award.OtherInstanceId, out entry))
                {
                    _glow.Add(entry, HighlightGlowRenderer.GlowStyle.Bonus);
                    _labels.Add(entry.LabelAnchor, $"+{award.Score} 物流互联", WindAwardLabelColor);
                }
                else if (upstream != null)
                {
                    _labels.Add(upstream.LabelAnchor, $"+{award.Score} 物流互联", WindAwardLabelColor);
                }
            }
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

        private void Apply(ScoreBreakdown breakdown, Vector3 selfAnchor, IReadOnlyList<WindAward> windAwards)
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
                        // 基础分挂在被摆建筑自己身上，风力分在 EmitSelfLabels 里逐条飘，这里只处理有实例的对象
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

            if (breakdown != null)
            {
                EmitSelfLabels(breakdown, selfAnchor);
            }
            if (windAwards != null && windAwards.Count > 0)
            {
                EmitWindAwards(windAwards);
            }

            _labels.End();
        }

        /// <summary>标签竖排步距（世界单位）。</summary>
        private const float SelfLabelStep = 0.7f;

        /// <summary>
        /// 被摆建筑自己身上的飘字（§7.4 + 用户定稿）：
        /// 顶行是「范围内所有加减分的总和」——即除基础分/孤立惩罚（Self 条目）外、
        /// 所有已计入归因的净和，邻居给的分与风给的分都算「范围内」；
        /// 建在风带上时，下面逐条列出风的加减分（风力即时分 / 强风穿过惩罚 / 物流风覆盖），
        /// 用与风流线同色系的字，玩家能把「这几分」与头顶那条风对上号。
        /// </summary>
        private void EmitSelfLabels(ScoreBreakdown breakdown, Vector3 selfAnchor)
        {
            IReadOnlyList<ScoreAttribution> attributions = breakdown.Attributions;

            int surroundTotal = 0;
            for (int i = 0; i < attributions.Count; i++)
            {
                ScoreAttribution attribution = attributions[i];
                if (attribution.Kind != ScoreSourceKind.Self && attribution.Counted)
                {
                    surroundTotal += attribution.Score;
                }
            }

            int stacked = 0;
            if (surroundTotal != 0)
            {
                bool bonus = surroundTotal > 0;
                _labels.Add(
                    selfAnchor,
                    $"周边合计 {(bonus ? "+" : "")}{surroundTotal}",
                    bonus ? BonusLabelColor : PenaltyLabelColor);
                stacked++;
            }

            for (int i = 0; i < attributions.Count; i++)
            {
                ScoreAttribution attribution = attributions[i];
                if (attribution.Kind != ScoreSourceKind.Wind || attribution.Score == 0)
                {
                    continue;
                }
                bool bonus = attribution.Score > 0;
                _labels.Add(
                    selfAnchor + Vector3.down * (SelfLabelStep * stacked),
                    $"{(bonus ? "+" : "")}{attribution.Score} {attribution.Label}",
                    bonus ? WindAwardLabelColor : PenaltyLabelColor);
                stacked++;
            }
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
