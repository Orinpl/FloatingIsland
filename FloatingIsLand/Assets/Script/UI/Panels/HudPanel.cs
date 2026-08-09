using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using UnityEngine;
using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 局内 HUD。布局按项目设计（GAME_DESIGN §7.4、PROJECT_BUILD §4.8）：
    ///
    /// - **左下角计分区**：总分 / 金币 / 当前等级 + 「解锁下一组建筑」按钮；
    /// - **屏幕中下建筑列表**：当前手牌，点一张即进入摆放模式（再点一次取消）；
    /// - 顶部保留关数信息与占位「结束本局」按钮。
    ///
    /// 面板是哑视图：只暴露控件与 Set 方法，所有行为由适配器绑定
    /// （流程按钮 → FlowUIAdapter，建造相关 → BuildHudAdapter）。
    /// </summary>
    public sealed class HudPanel : UIPanel
    {
        /// <summary>手牌条目的显示数据：名字分「是哪种建筑」，形状图标分「占地长什么样」。</summary>
        public readonly struct HandItemView
        {
            /// <summary>形状图标的缓存键，用变体 Id。</summary>
            public readonly string Key;

            /// <summary>显示名（配表 BuildingVariant.nameCn，空则回落 Building.nameCn）。</summary>
            public readonly string NameCn;

            /// <summary>占地掩码；null = 不画图标。</summary>
            public readonly Footprint Shape;

            public HandItemView(string key, string nameCn, Footprint shape)
            {
                Key = key;
                NameCn = nameCn;
                Shape = shape;
            }
        }

        /// <summary>手牌按钮里形状图标的最大占位（像素），按掩码长宽比等比缩放后放进去。</summary>
        private static readonly Vector2 ShapeBox = new Vector2(64f, 50f);

        /// <summary>手牌按钮底部留给名字的高度（像素）。图标占上面剩下的部分。</summary>
        private const float HandLabelHeight = 42f;

        private const int HandLabelFontSize = 20;

        [Header("顶部：关卡信息")]
        public Text runInfoText;
        public Button endRunButton;

        [Header("左下角：计分区")]
        public Text scoreText;
        public Text goldText;
        public Text levelText;
        public Button nextGroupButton;
        public Text nextGroupButtonLabel;

        [Header("中下：建筑列表（手牌）")]
        public RectTransform handRoot;
        public Button handItemTemplate;

        [Header("中部：建筑组二选一")]
        public RectTransform offerRoot;
        public Button offerItemTemplate;

        [Header("提示")]
        public Text hintText;

        private readonly List<Button> _handItems = new List<Button>();
        private readonly List<Button> _offerItems = new List<Button>();

        /// <summary>点击了手牌第 N 张。</summary>
        public event Action<int> HandItemClicked;

        /// <summary>点击了第 N 组建筑组。</summary>
        public event Action<int> OfferClicked;

        public void SetRunInfo(string info)
        {
            if (runInfoText != null)
            {
                runInfoText.text = info;
            }
        }

        /// <summary>刷新左下角计分区。</summary>
        public void SetScoreboard(int score, int gold, int level, int totalLevels, string nextGroupLabel, bool nextGroupInteractable)
        {
            if (scoreText != null)
            {
                scoreText.text = $"总分 {score}";
            }
            if (goldText != null)
            {
                goldText.text = $"金币 {gold}";
            }
            if (levelText != null)
            {
                levelText.text = $"等级 {level} / {totalLevels}";
            }
            if (nextGroupButtonLabel != null)
            {
                nextGroupButtonLabel.text = nextGroupLabel;
            }
            if (nextGroupButton != null)
            {
                nextGroupButton.interactable = nextGroupInteractable;
            }
        }

        /// <summary>刷新手牌列表。selectedIndex = -1 表示没有选中。</summary>
        public void SetHand(IReadOnlyList<HandItemView> items, int selectedIndex)
        {
            int count = items != null ? items.Count : 0;
            EnsureItems(_handItems, handRoot, handItemTemplate, count, HandItemClicked);

            for (int i = 0; i < _handItems.Count; i++)
            {
                Button item = _handItems[i];
                bool visible = i < count;
                item.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                HandItemView view = items[i];
                ApplyShapeIcon(item, view);
                SetItemLabel(item, view.NameCn, HandLabelFontSize, HandLabelHeight);
                SetItemTint(item, i == selectedIndex);
            }
        }

        /// <summary>刷新建筑组二选一；传空列表即隐藏该区域。</summary>
        public void SetOffers(IReadOnlyList<string> labels)
        {
            int count = labels != null ? labels.Count : 0;
            EnsureItems(_offerItems, offerRoot, offerItemTemplate, count, OfferClicked);

            for (int i = 0; i < _offerItems.Count; i++)
            {
                Button item = _offerItems[i];
                bool visible = i < count;
                item.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                SetItemLabel(item, labels[i], 0, 0f);
                SetItemTint(item, false);
            }

            if (offerRoot != null)
            {
                offerRoot.gameObject.SetActive(count > 0);
            }
        }

        /// <summary>设置提示文本（摆放预览得分 / 非法原因 / 操作提示）。</summary>
        public void SetHint(string hint)
        {
            if (hintText != null)
            {
                hintText.text = hint ?? string.Empty;
            }
        }

        /// <summary>
        /// 按需把一排按钮补足到 count 个。
        /// 模板本身保持隐藏并留在层级里当样板，实例化出来的才是真正显示的条目——
        /// 这样按钮样式改一处即可，不用在代码里手搓 RectTransform。
        /// </summary>
        private void EnsureItems(List<Button> items, RectTransform root, Button template, int count, Action<int> onClick)
        {
            if (root == null || template == null)
            {
                return;
            }

            template.gameObject.SetActive(false);

            while (items.Count < count)
            {
                Button item = Instantiate(template, root);
                int index = items.Count;
                item.onClick.AddListener(() => onClick?.Invoke(index));
                items.Add(item);
            }
        }

        private static void SetItemTint(Button item, bool selected)
        {
            var image = item.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? new Color(1f, 0.85f, 0.35f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.75f);
            }
        }

        /// <summary>
        /// 写条目文字。<paramref name="bandHeight"/> &gt; 0 时把文字压到按钮底部的固定高度条带里，
        /// 上面腾出来的空间留给形状图标；传 0 则维持模板的整块居中。
        /// </summary>
        private static void SetItemLabel(Button item, string text, int fontSize, float bandHeight)
        {
            var label = item.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                return;
            }

            label.text = text;
            if (fontSize > 0)
            {
                label.fontSize = fontSize;
            }

            if (bandHeight <= 0f)
            {
                return;
            }

            RectTransform rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-8f, bandHeight);
            rt.anchoredPosition = new Vector2(0f, 4f);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary>
        /// 在条目里补一张形状图标。模板是编辑器生成的（BootSceneBuilder），没有图标节点，
        /// 所以这里按需现建——避免为了加个图标就得重跑一遍场景生成菜单。
        /// </summary>
        private static void ApplyShapeIcon(Button item, HandItemView view)
        {
            Texture2D texture = FootprintIcon.Get(view.Key, view.Shape);

            Transform found = item.transform.Find("Shape");
            RawImage icon = found != null ? found.GetComponent<RawImage>() : null;
            if (icon == null)
            {
                if (texture == null)
                {
                    return;
                }

                var go = new GameObject("Shape", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(item.transform, false);
                icon = go.GetComponent<RawImage>();
                icon.raycastTarget = false; // 点击要落到按钮上，图标不能挡

                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -8f);
            }

            if (texture == null)
            {
                icon.gameObject.SetActive(false);
                return;
            }

            icon.gameObject.SetActive(true);
            icon.texture = texture;

            // 等比缩放塞进 ShapeBox：占地越大画得越小，玩家一眼能比出 6×6 船坞和 1×1 农田
            int cols = view.Shape.Columns;
            int rows = view.Shape.Rows;
            float scale = Mathf.Min(ShapeBox.x / cols, ShapeBox.y / rows);
            icon.rectTransform.sizeDelta = new Vector2(cols * scale, rows * scale);
        }
    }
}
