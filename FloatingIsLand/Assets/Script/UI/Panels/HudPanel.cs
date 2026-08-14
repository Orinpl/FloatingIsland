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
    /// - **屏幕中部选组区**：一组一排，排里直接摆出这组会给的建筑卡片（带略缩图）；
    /// - **右上角**：关数信息、「结束本关」（带本关分数进度，达标后呼吸高亮）、「返回主界面」；
    /// - **浮在待摆建筑头顶的触屏工具条**（旋转 / 建造 / 取消）：只在触屏 + 摆放模式下出现，
    ///   补上手机没有的滚轮、左键和 Esc（见 <see cref="SetTouchControls"/>）。
    ///   位置每帧跟着建筑走；指不到任何格子时才回落到屏幕右下角。
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

        /// <summary>选组一排里的一格：一张卡片 + 这一组里给几个。</summary>
        public readonly struct OfferItemView
        {
            public readonly HandItemView Item;

            /// <summary>本组里这个变体的份数（&gt;1 时名字后面缀 ×N）。</summary>
            public readonly int Count;

            public OfferItemView(HandItemView item, int count)
            {
                Item = item;
                Count = count;
            }

            /// <summary>同一变体又出现一次。结构体是只读的，所以返回新值而不是自增。</summary>
            public OfferItemView WithOneMore()
            {
                return new OfferItemView(Item, Count + 1);
            }
        }

        /// <summary>待选建筑组的显示数据：组名 + 选中它会进手牌的那批建筑。</summary>
        public readonly struct OfferView
        {
            /// <summary>主题名（BuildingGroup.NameCn），一排的标题。</summary>
            public readonly string NameCn;

            public readonly IReadOnlyList<OfferItemView> Items;

            public OfferView(string nameCn, IReadOnlyList<OfferItemView> items)
            {
                NameCn = nameCn;
                Items = items;
            }
        }

        /// <summary>
        /// 一张建筑卡片的排版。卡片自上而下三段：略缩图 / 形状图标 / 名字——
        /// 三样东西各答一个问题（长什么样 / 占几格 / 叫什么）。
        ///
        /// 手牌和「建筑组二选一」用的是同一种卡片，只是后者一排要塞下整组建筑所以小一圈，
        /// 于是把尺寸抽成参数，而不是把画法复制两份。
        ///
        /// 尺寸在运行时写死到实例上，而不是改 BootSceneBuilder 的模板——
        /// 改模板要重跑场景生成菜单（带模态弹窗），为了排个版不值当。
        /// </summary>
        private readonly struct CardLayout
        {
            /// <summary>卡片整体尺寸（像素，参考分辨率 1920×1080）。</summary>
            public readonly Vector2 Size;

            /// <summary>略缩图高度；宽度 = 卡片宽 − <see cref="ThumbMargin"/>×2。宽高比要和 BuildingThumbnail 的输出接近，否则会拉伸。</summary>
            public readonly float ThumbHeight;
            public readonly float ThumbMargin;
            public readonly float ThumbTop;

            /// <summary>形状图标的最大占位，按掩码长宽比等比缩放后放进去。</summary>
            public readonly Vector2 ShapeBox;

            /// <summary>卡片底部留给名字的高度。</summary>
            public readonly float LabelHeight;
            public readonly int LabelFontSize;

            public CardLayout(
                Vector2 size, float thumbHeight, float thumbMargin, float thumbTop,
                Vector2 shapeBox, float labelHeight, int labelFontSize)
            {
                Size = size;
                ThumbHeight = thumbHeight;
                ThumbMargin = thumbMargin;
                ThumbTop = thumbTop;
                ShapeBox = shapeBox;
                LabelHeight = labelHeight;
                LabelFontSize = labelFontSize;
            }
        }

        /// <summary>手牌卡片：玩家整局都在点的东西，画得最大。</summary>
        private static readonly CardLayout HandCard = new CardLayout(
            new Vector2(150f, 160f), 76f, 8f, 6f, new Vector2(60f, 32f), 36f, 20);

        /// <summary>选组卡片：一排要横着放下整组（可能六七个），比手牌小一圈。</summary>
        private static readonly CardLayout OfferCard = new CardLayout(
            new Vector2(124f, 142f), 64f, 6f, 5f, new Vector2(44f, 22f), 40f, 16);

        /// <summary>选组一排的尺寸。一排 = 一组，玩家横着扫一眼就知道这组给什么。</summary>
        private static readonly Vector2 OfferRowSize = new Vector2(1080f, 200f);

        /// <summary>排与排的间距，要和 BootSceneBuilder 里 VerticalLayoutGroup.spacing 对上。</summary>
        private const float OfferRowSpacing = 14f;

        /// <summary>一排顶部留给组名的高度。</summary>
        private const float OfferTitleHeight = 42f;
        private const int OfferTitleFontSize = 26;

        /// <summary>选组卡片之间的横向间距。</summary>
        private const float OfferCardGap = 8f;

        /// <summary>卡片区离一排四边的内边距。左右留白让木牌的收口端头露出来，别被卡片压住。</summary>
        private const float OfferCardsPadding = 22f;

        [Header("顶部：关卡信息")]
        public Text runInfoText;
        public Button endRunButton;
        public Text endRunButtonLabel;

        [Header("左下角：计分区")]
        public Text scoreText;
        /// <summary>本关通关进度（原金币栏；解锁货币换成分数后金币已彻底移除）。</summary>
        public Text clearProgressText;
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

        /// <summary>触屏工具条：转 90°。手机没有滚轮，旋转只能靠它。</summary>
        public event Action RotateClicked;

        /// <summary>触屏工具条：在当前落点建造。</summary>
        public event Action ConfirmClicked;

        /// <summary>触屏工具条：退出摆放模式。手机没有 Esc。</summary>
        public event Action CancelClicked;

        /// <summary>点了右上角「返回主界面」并完成二次确认（放弃本局）。</summary>
        public event Action HomeConfirmed;

        public void SetRunInfo(string info)
        {
            if (runInfoText != null)
            {
                runInfoText.text = info;
            }
        }

        /// <summary>
        /// 刷新右上角「收官」按钮。两行：
        /// 第一行是这一下点下去会发生什么（达通关分后才是「进入下一关」，否则是提前认输），
        /// 第二行是本关分数进度「当前 / 通关线」——玩家不用再去左下角对着计分区自己算还差多少。
        ///
        /// 达标后按钮开始呼吸高亮（见 <see cref="UpdateEndRunPulse"/>）：这时候「可以走了」是
        /// 玩家最需要知道的一件事，而右上角平时没人看，光换文案推不动。
        /// <paramref name="clearScore"/> &lt;= 0（还没装载完地图）时只写第一行。
        /// </summary>
        public void SetEndRunButton(string label, int totalScore, int clearScore, bool cleared)
        {
            EnsureTopRightButtons();

            if (endRunButtonLabel != null)
            {
                endRunButtonLabel.text = clearScore > 0
                    ? $"{label}\n{totalScore} / {clearScore} 分{(cleared ? " ✔" : string.Empty)}"
                    : label;
            }

            if (_endRunHighlight != cleared)
            {
                _endRunHighlight = cleared;
                if (!cleared)
                {
                    ResetEndRunTint();
                }
            }
        }

        /// <summary>
        /// 刷新左下角计分区。<paramref name="totalScore"/> 是跨关累计分，
        /// <paramref name="clearScore"/> 是本关通关门槛（同样是累计分口径），两者直接可比。
        /// </summary>
        public void SetScoreboard(
            int totalScore, int clearScore, bool stageCleared,
            int group, int groupTotal, string nextGroupLabel, bool nextGroupInteractable)
        {
            if (scoreText != null)
            {
                scoreText.text = $"总分 {totalScore}";
            }
            if (clearProgressText != null)
            {
                clearProgressText.text = stageCleared
                    ? $"通关达标 {clearScore} ✔"
                    : $"通关需 {clearScore}（还差 {clearScore - totalScore}）";
            }
            if (levelText != null)
            {
                levelText.text = $"第 {group} / {groupTotal} 组";
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

                ApplyCardSize((RectTransform)item.transform, item.GetComponent<LayoutElement>(), HandCard.Size);
                ApplyCard(item.transform, items[i], HandCard, 1);
                SetItemTint(item, i == selectedIndex);
            }
        }

        /// <summary>
        /// 刷新建筑组二选一；传空列表即隐藏该区域。
        ///
        /// 一组一排，排里直接把「选了会进手牌的那些建筑」摆成卡片。
        /// 早先这里是一行文字（「【市民中心区】市民中心 居民区 方形×2 …」），
        /// 但玩家在这一步要判断的是「这组能不能摆得下、好不好连片」——那是形状和体量的问题，
        /// 名字堆成一行读起来像配表，看完还是不知道拿到手是什么。
        /// </summary>
        public void SetOffers(IReadOnlyList<OfferView> offers)
        {
            int count = offers != null ? offers.Count : 0;
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

                ApplyOfferRow(item, offers[i]);
            }

            if (offerRoot == null)
            {
                return;
            }

            offerRoot.gameObject.SetActive(count > 0);
            if (count > 0)
            {
                // 整块居中显示，所以高度得跟着排数走，否则两排会从模板那 120 高的框里溢出去
                offerRoot.sizeDelta = new Vector2(
                    OfferRowSize.x, count * OfferRowSize.y + (count - 1) * OfferRowSpacing);
            }
        }

        /// <summary>把一排画成「组名 + 一横排建筑卡片」。整排都是按钮，点哪儿都算选中这一组。</summary>
        private static void ApplyOfferRow(Button row, OfferView view)
        {
            ApplyCardSize((RectTransform)row.transform, row.GetComponent<LayoutElement>(), OfferRowSize);
            SetItemTint(row, false);

            // 组名压到顶部一条：主题名放最前，二选一的意义是「选一条路线」，
            // 光看建筑清单玩家得自己去脑补这组想干什么
            Text title = FindChildText(row.transform, "Text");
            if (title != null)
            {
                title.text = view.NameCn;
                title.fontSize = OfferTitleFontSize;
                title.alignment = TextAnchor.MiddleCenter;
                RectTransform titleRt = title.rectTransform;
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.sizeDelta = new Vector2(-OfferCardsPadding * 2f, OfferTitleHeight);
                titleRt.anchoredPosition = new Vector2(0f, -6f);
            }

            RectTransform cards = EnsureCardsRow(row.transform);
            int count = view.Items != null ? view.Items.Count : 0;
            EnsureOfferCards(cards, count);

            for (int i = 0; i < cards.childCount; i++)
            {
                Transform card = cards.GetChild(i);
                bool visible = i < count;
                card.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                OfferItemView entry = view.Items[i];
                ApplyCard(card, entry.Item, OfferCard, entry.Count);
            }
        }

        /// <summary>一排里装卡片的横条（组名下方那块）。按需现建，理由同略缩图：不为排版重跑场景生成。</summary>
        private static RectTransform EnsureCardsRow(Transform row)
        {
            Transform found = row.Find("Cards");
            if (found != null)
            {
                return (RectTransform)found;
            }

            var go = new GameObject("Cards", typeof(RectTransform));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(OfferCardsPadding, OfferCardsPadding * 0.5f);
            rt.offsetMax = new Vector2(-OfferCardsPadding, -OfferTitleHeight);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = OfferCardGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>把卡片横条补足到 count 张。卡片本身不是按钮——点击要落到整排上。</summary>
        private static void EnsureOfferCards(RectTransform cards, int count)
        {
            while (cards.childCount < count)
            {
                var go = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(cards, false);

                var image = go.GetComponent<Image>();
                Sprite sprite = UISkin.Get(UIStyle.Card);
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                }
                image.color = Color.white;
                image.raycastTarget = false;

                var layout = go.AddComponent<LayoutElement>();
                layout.preferredWidth = OfferCard.Size.x;
                layout.preferredHeight = OfferCard.Size.y;
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

        /// <summary>米色卡片底叠一层暖金表示选中。</summary>
        private static readonly Color HandItemSelected = new Color(1f, 0.80f, 0.42f, 1f);

        /// <summary>
        /// 选中态染色。染的是按钮的 <see cref="ColorBlock"/> 而不只是 <c>Image.color</c>：
        /// 后者会在鼠标划过时被 Button 的状态过渡覆写回 normalColor，选中高亮就没了。
        /// </summary>
        private static void SetItemTint(Button item, bool selected)
        {
            Color tint = selected ? HandItemSelected : Color.white;

            ColorBlock colors = item.colors;
            colors.normalColor = tint;
            colors.selectedColor = tint;
            colors.highlightedColor = tint * 1.08f;
            colors.pressedColor = tint * 0.86f;
            item.colors = colors;

            var image = item.GetComponent<Image>();
            if (image != null)
            {
                image.color = tint; // 立刻生效，不用等下一次状态过渡
            }
        }

        /// <summary>画一张卡片：略缩图 / 形状图标 / 名字三段。<paramref name="count"/> &gt; 1 时名字后面缀 ×N。</summary>
        private static void ApplyCard(Transform card, HandItemView view, CardLayout layout, int count)
        {
            ApplyThumbnail(card, view, layout);
            ApplyShapeIcon(card, view, layout);
            SetCardLabel(card, count > 1 ? $"{view.NameCn} ×{count}" : view.NameCn, layout);
        }

        /// <summary>
        /// 写卡片底部的名字。文字压到固定高度的条带里，上面腾出来的空间留给略缩图和形状图标。
        /// </summary>
        private static void SetCardLabel(Transform card, string text, CardLayout layout)
        {
            Text label = EnsureCardLabel(card);
            if (label == null)
            {
                return;
            }

            label.text = text;
            label.fontSize = layout.LabelFontSize;

            RectTransform rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-8f, layout.LabelHeight);
            rt.anchoredPosition = new Vector2(0f, 4f);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary>
        /// 取卡片上的名字节点。手牌卡片是按钮模板克隆的，字在 "Text"；
        /// 选组卡片是现建的，第一次画时补一个 "Label"。
        /// </summary>
        private static Text EnsureCardLabel(Transform card)
        {
            Text existing = FindChildText(card, "Text");
            if (existing != null)
            {
                return existing;
            }
            existing = FindChildText(card, "Label");
            if (existing != null)
            {
                return existing;
            }
            return CreateLabel(card, "Label", OfferCard.LabelFontSize, UIStyle.Card);
        }

        private static Text FindChildText(Transform parent, string childName)
        {
            Transform found = parent.Find(childName);
            return found != null ? found.GetComponent<Text>() : null;
        }

        /// <summary>
        /// 把条目撑到目标尺寸。模板是 BootSceneBuilder 生成的小方块，塞不下卡片的三段内容，
        /// 所以在实例上改（模板不动，改模板要重跑场景生成菜单）。
        /// </summary>
        private static void ApplyCardSize(RectTransform rt, LayoutElement layout, Vector2 size)
        {
            if (rt.sizeDelta != size)
            {
                rt.sizeDelta = size;
            }

            // 布局组认的是 LayoutElement，不改这个的话卡片会被排回模板尺寸
            if (layout != null)
            {
                layout.preferredWidth = size.x;
                layout.preferredHeight = size.y;
            }
        }

        /// <summary>顶部的建筑略缩图。模型缺失（白模阶段）时整块收起，下面两段照常显示。</summary>
        private static void ApplyThumbnail(Transform card, HandItemView view, CardLayout layout)
        {
            Texture texture = BuildingThumbnail.Get(view.Key, view.PrefabPath);

            RawImage thumb = EnsureRawImage(card, "Thumb", texture != null);
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
            rt.sizeDelta = new Vector2(-layout.ThumbMargin * 2f, layout.ThumbHeight);
            rt.anchoredPosition = new Vector2(0f, -layout.ThumbTop);
        }

        /// <summary>
        /// 略缩图下方的形状图标。模板是编辑器生成的（BootSceneBuilder），没有这些节点，
        /// 所以按需现建——避免为了排个版就得重跑一遍场景生成菜单。
        /// </summary>
        private static void ApplyShapeIcon(Transform card, HandItemView view, CardLayout layout)
        {
            Texture2D texture = FootprintIcon.Get(view.Key, view.Shape);

            RawImage icon = EnsureRawImage(card, "Shape", texture != null);
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
            rt.anchoredPosition = new Vector2(0f, -(layout.ThumbTop + layout.ThumbHeight + 6f));

            // 等比缩放塞进 ShapeBox：占地越大画得越小，玩家一眼能比出 6×6 船坞和 1×1 农田
            int cols = view.Shape.Columns;
            int rows = view.Shape.Rows;
            float scale = Mathf.Min(layout.ShapeBox.x / cols, layout.ShapeBox.y / rows);
            rt.sizeDelta = new Vector2(cols * scale, rows * scale);
        }

        // ── 触屏工具条 ────────────────────────────────────────────────────────
        //
        // 手机上缺三个 PC 有的动作：滚轮转 90°、左键落地、Esc 取消。补成三个按钮。
        // 整条工具条运行时现建，理由和上面的略缩图 / 形状图标一样——场景是 BootSceneBuilder
        // 生成的，往模板里加节点就得重跑那条带模态弹窗的生成流程，而这三个按钮没有任何
        // 需要美术在 Inspector 里调的引用。
        //
        // 工具条**跟着待摆建筑走，浮在它头顶**，不再钉在屏幕右下角。原因是手机屏幕小：
        // 钉在角上的话，玩家的视线要在「楼在哪」和「按钮在哪」之间来回跳，而且拇指伸过去
        // 的路上正好盖住刚摆好的楼。浮在楼头顶则是「看哪儿点哪儿」，也顺带避开了手指遮挡区。

        /// <summary>按钮尺寸（像素，参考分辨率 1920×1080）。够一根手指按，不用瞄。</summary>
        private static readonly Vector2 TouchButtonSize = new Vector2(150f, 92f);

        private const float TouchBarMargin = 24f;
        private const float TouchBarGap = 12f;

        /// <summary>
        /// 没有落点时工具条回落到的高度（抬离屏幕底部）：要压在手牌条上方，不然会挡住手牌。
        /// 只在「摆放中但指不到任何格子」时用得上——那种情况下没有楼可以浮，
        /// 但「取消」必须还够得着，否则玩家会被卡在摆放模式里出不来。
        /// </summary>
        private const float TouchBarBottom = 190f;

        /// <summary>工具条底边离建筑头顶锚点的额外像素间距。锚点本身已在世界空间里让开了飘分数字。</summary>
        private const float TouchBarAnchorPadding = 8f;

        private const int TouchButtonFontSize = 26;

        private RectTransform _touchBar;
        private Button _confirmButton;
        private Text _confirmLabel;

        /// <summary>
        /// 显隐并摆放触屏工具条。只在触屏模式且正在摆放时显示：鼠标玩家不需要它，
        /// 摆放模式外也没有可确认的东西。
        /// </summary>
        /// <param name="canConfirm">当前落点合法（决定「建造」按钮灰不灰）。</param>
        /// <param name="hasAnchor">
        /// 有没有算出建筑头顶在屏幕上的位置。false 时（没落点，或楼被转到了相机背后）
        /// 回落到屏幕右下角——宁可位置不理想，也不能让「取消」消失。
        /// </param>
        /// <param name="screenAnchor">建筑头顶的屏幕坐标（像素）。</param>
        public void SetTouchControls(
            bool visible, bool canConfirm, string confirmLabel, bool hasAnchor, Vector2 screenAnchor)
        {
            if (!visible)
            {
                if (_touchBar != null)
                {
                    _touchBar.gameObject.SetActive(false);
                }
                return;
            }

            EnsureTouchBar();
            _touchBar.gameObject.SetActive(true);
            PositionTouchBar(hasAnchor, screenAnchor);
            if (_confirmButton != null)
            {
                _confirmButton.interactable = canConfirm;
            }
            if (_confirmLabel != null)
            {
                _confirmLabel.text = confirmLabel;
            }
        }

        /// <summary>
        /// 把工具条摆到建筑头顶，并保证整条都还在屏幕里。
        ///
        /// 坐标换算全部走「以面板矩形左下角为原点」：工具条锚点定在 (0,0)，于是
        /// anchoredPosition 就是离左下角的偏移，与面板自身的 pivot 无关——
        /// 面板是全屏拉伸的，但不该把它的 pivot 恰好是 (0.5,0.5) 这件事写进算式里。
        ///
        /// 限幅是必须的：楼可以被拖到屏幕边缘甚至画面外，工具条跟出去就点不着了。
        /// 贴边时宁可让它压在楼旁边，也好过跟着飞出屏幕。
        /// </summary>
        private void PositionTouchBar(bool hasAnchor, Vector2 screenAnchor)
        {
            var panelRect = transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            Rect area = panelRect.rect;
            Vector2 size = _touchBar.sizeDelta;
            float halfWidth = size.x * 0.5f;

            // Canvas 是 ScreenSpaceOverlay，所以取局部坐标时第三个参数（相机）必须传 null
            Vector2 local;
            Vector2 target;
            if (hasAnchor
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    panelRect, screenAnchor, null, out local))
            {
                target = (local - area.min) + new Vector2(0f, TouchBarAnchorPadding);
            }
            else
            {
                // 回落到右下角，与改造前同一个位置
                target = new Vector2(area.width - TouchBarMargin - halfWidth, TouchBarBottom);
            }

            target.x = Mathf.Clamp(target.x, TouchBarMargin + halfWidth, area.width - TouchBarMargin - halfWidth);
            target.y = Mathf.Clamp(target.y, TouchBarMargin, area.height - TouchBarMargin - size.y);
            _touchBar.anchoredPosition = target;
        }

        private void EnsureTouchBar()
        {
            if (_touchBar != null)
            {
                return;
            }

            var barGo = new GameObject("TouchControls", typeof(RectTransform));
            barGo.transform.SetParent(transform, false);
            _touchBar = (RectTransform)barGo.transform;
            // 锚在面板左下角、自身 pivot 取底边中点：位置每帧由 PositionTouchBar 算，
            // 底边中点让「浮在楼正上方」变成直接把投影点写进 anchoredPosition
            _touchBar.anchorMin = Vector2.zero;
            _touchBar.anchorMax = Vector2.zero;
            _touchBar.pivot = new Vector2(0.5f, 0f);
            _touchBar.sizeDelta = new Vector2(
                TouchButtonSize.x * 3f + TouchBarGap * 2f, TouchButtonSize.y);

            // 从右往左：建造（最常按，放拇指最舒服的位置）→ 旋转 → 取消。
            // 皮肤按语义分：建造是「往前走」用主木牌，取消是「退一步」用浅木条，
            // 旋转是纯图标操作用方块——三个按钮光靠形状和色温就能分开，不用先读字。
            Text confirmLabel;
            _confirmButton = CreateTouchButton("Confirm", "建造", 0, UIStyle.Primary, out confirmLabel);
            _confirmLabel = confirmLabel;
            _confirmButton.onClick.AddListener(() => { Action h = ConfirmClicked; if (h != null) { h(); } });

            Text ignored;
            Button rotate = CreateTouchButton("Rotate", "旋转 ↻", 1, UIStyle.Icon, out ignored);
            rotate.onClick.AddListener(() => { Action h = RotateClicked; if (h != null) { h(); } });

            Button cancel = CreateTouchButton("Cancel", "取消", 2, UIStyle.Secondary, out ignored);
            cancel.onClick.AddListener(() => { Action h = CancelClicked; if (h != null) { h(); } });
        }

        /// <summary><paramref name="slotFromRight"/> = 从右往左第几个位置（0 起）。</summary>
        private Button CreateTouchButton(string name, string label, int slotFromRight, UIStyle style, out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(_touchBar, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = TouchButtonSize;
            rt.anchoredPosition = new Vector2(-slotFromRight * (TouchButtonSize.x + TouchBarGap), 0f);

            labelText = CreateLabel(go.transform, "Label", TouchButtonFontSize, style);
            labelText.text = label;

            Button button = go.GetComponent<Button>();
            // 工具条是运行时现建的，赶不上 UIManager.Awake 那趟统一刷皮，自己套一次
            UISkin.ApplyButton(button, style);
            return button;
        }

        /// <summary>
        /// 铺满父节点的文字节点。运行时现建的按钮 / 卡片都用它，
        /// 省得每处再手搓一遍字体、锚点和 raycastTarget。
        /// </summary>
        private static Text CreateLabel(Transform parent, string name, int fontSize, UIStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = UISkin.TextColor(style);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false; // 点击要落到按钮上，字不能挡
            UISkin.ApplyText(text); // 现建的节点赶不上 UIManager.Awake 那趟统一刷字重
            return text;
        }

        /// <summary>取（必要时现建）卡片下的一个 RawImage 子节点。没内容可显示又还没建过时返回 null。</summary>
        private static RawImage EnsureRawImage(Transform card, string childName, bool createIfMissing)
        {
            Transform found = card.Find(childName);
            if (found != null)
            {
                return found.GetComponent<RawImage>();
            }
            if (!createIfMissing)
            {
                return null;
            }

            var go = new GameObject(childName, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(card, false);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false; // 点击要落到按钮上，图不能挡
            return image;
        }

        // ── 右上角：结束本关 / 返回主界面 ────────────────────────────────────
        //
        // 两个按钮竖着排在右上角。「结束本关」自带本关分数进度，达标后呼吸高亮；
        // 「返回主界面」等于放弃本局，所以走两下确认。
        // 和触屏工具条一样运行时现建（场景由 BootSceneBuilder 生成，加节点要重跑那条带模态弹窗的流程）。

        /// <summary>「结束本关」要显示两行（动作 + 分数进度），比场景里那个通用按钮高一截。</summary>
        private static readonly Vector2 EndRunButtonSize = new Vector2(320f, 88f);
        private static readonly Vector2 HomeButtonSize = new Vector2(320f, 56f);
        private const float TopRightGap = 10f;
        private const int EndRunFontSize = 22;
        private const int HomeFontSize = 24;

        /// <summary>达标后「结束本关」的呼吸高亮色，和手牌选中同一支暖金。</summary>
        private static readonly Color EndRunHighlight = new Color(1f, 0.82f, 0.36f, 1f);

        /// <summary>呼吸一个来回的秒数。慢到不烦人，又快到不会被当成静止。</summary>
        private const float EndRunPulsePeriod = 1.2f;

        /// <summary>「返回主界面」二次确认的等待时长（秒）。超时自动退回，不会把按钮永远卡在确认态。</summary>
        private const float HomeConfirmWindow = 3f;

        private Button _homeButton;
        private Text _homeLabel;
        private Image _endRunImage;
        private bool _endRunHighlight;

        /// <summary>&gt; 0 表示正等第二下确认，值是确认窗口的截止时刻（<c>Time.unscaledTime</c> 口径）。</summary>
        private float _homeConfirmUntil;

        private void Update()
        {
            UpdateEndRunPulse();
            UpdateHomeConfirm();
        }

        private void OnDisable()
        {
            // 离开局内时把两个按钮恢复原状：下次进来不该还停在「确认放弃？」或高亮上
            _homeConfirmUntil = 0f;
            SetHomeLabel(false);
            ResetEndRunTint();
        }

        private void EnsureTopRightButtons()
        {
            if (_homeButton != null || endRunButton == null)
            {
                return;
            }

            var endRt = (RectTransform)endRunButton.transform;
            endRt.sizeDelta = EndRunButtonSize;
            _endRunImage = endRunButton.GetComponent<Image>();
            if (endRunButtonLabel != null)
            {
                endRunButtonLabel.fontSize = EndRunFontSize;
                endRunButtonLabel.verticalOverflow = VerticalWrapMode.Overflow;
            }

            // 名字取 MenuButton：UISkin 按名字派皮，和结算面板那个「回主界面」共用同一支浅木条
            var go = new GameObject("MenuButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(endRt.parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = endRt.anchorMin;
            rt.anchorMax = endRt.anchorMax;
            rt.pivot = endRt.pivot;
            rt.sizeDelta = HomeButtonSize;
            // 贴在「结束本关」正下方：位置跟着它算，省得两处各写一遍右上角的边距
            rt.anchoredPosition = endRt.anchoredPosition - new Vector2(0f, EndRunButtonSize.y + TopRightGap);

            _homeLabel = CreateLabel(go.transform, "Text", HomeFontSize, UIStyle.Secondary);
            _homeButton = go.GetComponent<Button>();
            _homeButton.onClick.AddListener(OnHomeClicked);
            UISkin.ApplyButton(_homeButton, UIStyle.Secondary);
            SetHomeLabel(false);
        }

        /// <summary>
        /// 「返回主界面」= 放弃本局，分数不结算。误触代价太大，所以要点两下：
        /// 第一下把按钮自己变成确认提示（而不是弹一个盖住画面的对话框），第二下才真走。
        /// </summary>
        private void OnHomeClicked()
        {
            if (_homeConfirmUntil > 0f && Time.unscaledTime < _homeConfirmUntil)
            {
                _homeConfirmUntil = 0f;
                SetHomeLabel(false);
                Action handler = HomeConfirmed;
                if (handler != null)
                {
                    handler();
                }
                return;
            }

            _homeConfirmUntil = Time.unscaledTime + HomeConfirmWindow;
            SetHomeLabel(true);
        }

        private void UpdateHomeConfirm()
        {
            if (_homeConfirmUntil <= 0f || Time.unscaledTime < _homeConfirmUntil)
            {
                return;
            }
            _homeConfirmUntil = 0f;
            SetHomeLabel(false);
        }

        private void SetHomeLabel(bool confirming)
        {
            if (_homeLabel == null)
            {
                return;
            }
            _homeLabel.text = confirming ? "再点一次放弃本局" : "返回主界面";
        }

        /// <summary>达通关分后让按钮呼吸。用 unscaledTime：局内暂停时它也该继续闪。</summary>
        private void UpdateEndRunPulse()
        {
            if (!_endRunHighlight || _endRunImage == null)
            {
                return;
            }

            float t = Mathf.PingPong(Time.unscaledTime / (EndRunPulsePeriod * 0.5f), 1f);
            _endRunImage.color = Color.Lerp(Color.white, EndRunHighlight, t);
        }

        /// <summary>停止呼吸并还原底色。白色是 UISkin 的约定底色——木牌是靠贴图上色的，染色会把木头染歪。</summary>
        private void ResetEndRunTint()
        {
            if (_endRunImage != null)
            {
                _endRunImage.color = Color.white;
            }
        }
    }
}
