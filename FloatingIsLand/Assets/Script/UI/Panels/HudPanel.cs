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
        /// <summary>
        /// 手牌条目的显示数据。三样东西各答一个问题：
        /// 略缩图 = 长什么样，形状图标 = 占几格什么形状，名字 = 叫什么。
        /// </summary>
        public readonly struct HandItemView
        {
            /// <summary>略缩图与形状图标的缓存键，用变体 Id。</summary>
            public readonly string Key;

            /// <summary>显示名（配表 BuildingVariant.nameCn，空则回落 Building.nameCn）。</summary>
            public readonly string NameCn;

            /// <summary>占地掩码；null = 不画形状图标。</summary>
            public readonly Footprint Shape;

            /// <summary>模型 Prefab 路径（配表 prefabPath）；空或资源缺失 = 不画略缩图。</summary>
            public readonly string PrefabPath;

            public HandItemView(string key, string nameCn, Footprint shape, string prefabPath)
            {
                Key = key;
                NameCn = nameCn;
                Shape = shape;
                PrefabPath = prefabPath;
            }
        }

        // 手牌卡片自上而下三段：略缩图 / 形状图标 / 名字。
        // 尺寸在运行时写死到实例上，而不是改 BootSceneBuilder 的模板——
        // 改模板要重跑场景生成菜单（带模态弹窗），为了排个版不值当。
        private static readonly Vector2 HandItemSize = new Vector2(150f, 160f);

        /// <summary>略缩图占位：左右各留 8px 边距，高度 76。宽高比要和 BuildingThumbnail 的输出接近，否则会拉伸。</summary>
        private const float ThumbHeight = 76f;
        private const float ThumbMargin = 8f;
        private const float ThumbTop = 6f;

        /// <summary>形状图标的最大占位（像素），按掩码长宽比等比缩放后放进去。</summary>
        private static readonly Vector2 ShapeBox = new Vector2(60f, 32f);

        /// <summary>手牌按钮底部留给名字的高度（像素）。</summary>
        private const float HandLabelHeight = 36f;

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
                ApplyHandItemSize(item);
                ApplyThumbnail(item, view);
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
        /// 把条目撑到手牌卡片的尺寸。模板是 BootSceneBuilder 生成的小方块，
        /// 塞不下「略缩图 + 形状 + 名字」三段，所以在实例上改（模板不动，改模板要重跑场景生成菜单）。
        /// </summary>
        private static void ApplyHandItemSize(Button item)
        {
            var rt = (RectTransform)item.transform;
            if (rt.sizeDelta != HandItemSize)
            {
                rt.sizeDelta = HandItemSize;
            }

            // HorizontalLayoutGroup 认的是 LayoutElement，不改这个的话卡片会被排回模板尺寸
            var layout = item.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = HandItemSize.x;
                layout.preferredHeight = HandItemSize.y;
            }
        }

        /// <summary>顶部的建筑略缩图。模型缺失（白模阶段）时整块收起，下面两段照常显示。</summary>
        private static void ApplyThumbnail(Button item, HandItemView view)
        {
            Texture texture = BuildingThumbnail.Get(view.Key, view.PrefabPath);

            RawImage thumb = EnsureRawImage(item, "Thumb", texture != null);
            if (thumb == null)
            {
                return;
            }

            if (texture == null)
            {
                thumb.gameObject.SetActive(false);
                return;
            }

            thumb.gameObject.SetActive(true);
            thumb.texture = texture;

            RectTransform rt = thumb.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-ThumbMargin * 2f, ThumbHeight);
            rt.anchoredPosition = new Vector2(0f, -ThumbTop);
        }

        /// <summary>
        /// 略缩图下方的形状图标。模板是编辑器生成的（BootSceneBuilder），没有这些节点，
        /// 所以按需现建——避免为了排个版就得重跑一遍场景生成菜单。
        /// </summary>
        private static void ApplyShapeIcon(Button item, HandItemView view)
        {
            Texture2D texture = FootprintIcon.Get(view.Key, view.Shape);

            RawImage icon = EnsureRawImage(item, "Shape", texture != null);
            if (icon == null)
            {
                return;
            }

            if (texture == null)
            {
                icon.gameObject.SetActive(false);
                return;
            }

            icon.gameObject.SetActive(true);
            icon.texture = texture;

            RectTransform rt = icon.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(ThumbTop + ThumbHeight + 6f));

            // 等比缩放塞进 ShapeBox：占地越大画得越小，玩家一眼能比出 6×6 船坞和 1×1 农田
            int cols = view.Shape.Columns;
            int rows = view.Shape.Rows;
            float scale = Mathf.Min(ShapeBox.x / cols, ShapeBox.y / rows);
            rt.sizeDelta = new Vector2(cols * scale, rows * scale);
        }

        /// <summary>取（必要时现建）条目下的一个 RawImage 子节点。没内容可显示又还没建过时返回 null。</summary>
        private static RawImage EnsureRawImage(Button item, string childName, bool createIfMissing)
        {
            Transform found = item.transform.Find(childName);
            if (found != null)
            {
                return found.GetComponent<RawImage>();
            }
            if (!createIfMissing)
            {
                return null;
            }

            var go = new GameObject(childName, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(item.transform, false);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false; // 点击要落到按钮上，图不能挡
            return image;
        }
    }
}
