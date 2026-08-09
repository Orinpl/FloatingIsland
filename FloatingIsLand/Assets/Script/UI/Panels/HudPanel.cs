using System;
using System.Collections.Generic;
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
        public void SetHand(IReadOnlyList<string> labels, int selectedIndex)
        {
            RebuildList(_handItems, handRoot, handItemTemplate, labels, selectedIndex, HandItemClicked);
        }

        /// <summary>刷新建筑组二选一；传空列表即隐藏该区域。</summary>
        public void SetOffers(IReadOnlyList<string> labels)
        {
            RebuildList(_offerItems, offerRoot, offerItemTemplate, labels, -1, OfferClicked);
            if (offerRoot != null)
            {
                offerRoot.gameObject.SetActive(labels != null && labels.Count > 0);
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
        /// 按标签列表重建一排按钮。
        /// 模板本身保持隐藏并留在层级里当样板，实例化出来的才是真正显示的条目——
        /// 这样按钮样式改一处即可，不用在代码里手搓 RectTransform。
        /// </summary>
        private void RebuildList(
            List<Button> items, RectTransform root, Button template,
            IReadOnlyList<string> labels, int selectedIndex, Action<int> onClick)
        {
            if (root == null || template == null)
            {
                return;
            }

            template.gameObject.SetActive(false);

            int count = labels != null ? labels.Count : 0;
            while (items.Count < count)
            {
                Button item = Instantiate(template, root);
                int index = items.Count;
                item.onClick.AddListener(() => onClick?.Invoke(index));
                items.Add(item);
            }

            for (int i = 0; i < items.Count; i++)
            {
                Button item = items[i];
                bool visible = i < count;
                item.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var label = item.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = labels[i];
                }

                var image = item.GetComponent<Image>();
                if (image != null)
                {
                    image.color = i == selectedIndex
                        ? new Color(1f, 0.85f, 0.35f, 0.95f)
                        : new Color(1f, 1f, 1f, 0.75f);
                }
            }
        }
    }
}
