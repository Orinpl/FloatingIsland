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
    /// - 顶部保留关数信息与占位「结束本局」按钮；
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

        public void SetRunInfo(string info)
        {
            if (runInfoText != null)
            {
                runInfoText.text = info;
            }
        }

        /// <summary>刷新右上角「收官」按钮：达通关分后它才是「进入下一关」，否则是「提前结束」。</summary>
        public void SetEndRunButton(string label)
        {
            if (endRunButtonLabel != null)
            {
                endRunButtonLabel.text = label;
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

            // 从右往左：建造（最常按，放拇指最舒服的位置）→ 旋转 → 取消
            Text confirmLabel;
            _confirmButton = CreateTouchButton("Confirm", "建造", 0, new Color(0.24f, 0.68f, 0.36f, 0.95f), out confirmLabel);
            _confirmLabel = confirmLabel;
            _confirmButton.onClick.AddListener(() => { Action h = ConfirmClicked; if (h != null) { h(); } });

            Text ignored;
            Button rotate = CreateTouchButton("Rotate", "旋转 ↻", 1, new Color(0.25f, 0.44f, 0.72f, 0.95f), out ignored);
            rotate.onClick.AddListener(() => { Action h = RotateClicked; if (h != null) { h(); } });

            Button cancel = CreateTouchButton("Cancel", "取消", 2, new Color(0.42f, 0.44f, 0.5f, 0.95f), out ignored);
            cancel.onClick.AddListener(() => { Action h = CancelClicked; if (h != null) { h(); } });
        }

        /// <summary><paramref name="slotFromRight"/> = 从右往左第几个位置（0 起）。</summary>
        private Button CreateTouchButton(string name, string label, int slotFromRight, Color color, out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(_touchBar, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = TouchButtonSize;
            rt.anchoredPosition = new Vector2(-slotFromRight * (TouchButtonSize.x + TouchBarGap), 0f);

            go.GetComponent<Image>().color = color;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            labelText = textGo.GetComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = TouchButtonFontSize;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.raycastTarget = false; // 点击要落到按钮上，字不能挡

            return go.GetComponent<Button>();
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
