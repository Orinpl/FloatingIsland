using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>按钮／底板的视觉风格。决定用哪张九宫格图 + 配什么颜色的字。</summary>
    public enum UIStyle
    {
        /// <summary>主行动按钮：橙色木牌。开始 / 建造 / 下一关 / 解锁 这类「往前走」的操作。</summary>
        Primary,

        /// <summary>次要按钮：浅色木条。返回 / 取消 / 退出 这类「退一步」的操作。</summary>
        Secondary,

        /// <summary>方形小按钮：橙色方块。工具条上的图标按钮（旋转）。</summary>
        Icon,

        /// <summary>卡片底：米色块。手牌那种自己不是「按钮长相」、靠内容说话的条目。</summary>
        Card,

        /// <summary>面板底：同米色块，但字色更深一档，配长段文字。</summary>
        Panel,
    }

    /// <summary>
    /// 把 <c>Assets/ArtRes/UI</c> 那套手绘木质 UI 套到运行时控件上。
    ///
    /// 原画是 3508×2480 的大图、画面只占中间一小块，直接当 Sprite 用会又糊又浪费；
    /// 已经离线加工成裁好、缩好、切好九宫格的图放在 <c>Assets/Resources/UI/</c>
    /// （加工脚本 <c>Tools/ArtGen/ui_slice_gen.py</c>，可重跑且 GUID 稳定；
    /// 九宫格 border 连同导入设置一起写死在各自的 .meta 里，不靠人在 Inspector 里拖）。
    ///
    /// 为什么是「运行时刷一遍」而不是改 BootSceneBuilder：
    /// Boot 场景是编辑器菜单生成的，改生成器就得重跑那条带模态弹窗的流程
    /// （HudPanel 里的略缩图 / 形状图标 / 触屏工具条也都是出于同一理由现建的）。
    /// 这里在 <see cref="UIManager"/> 的 Awake 里对整棵 UI 树刷一遍，
    /// 已生成的场景不用动，新加的按钮只要名字进表就自动有皮。
    ///
    /// 列表模板（HandItemTemplate / OfferItemTemplate）也在这一遍里被刷到，
    /// 之后 Instantiate 出来的条目天然带皮——前提是刷皮早于克隆，Awake 满足这一点。
    /// </summary>
    public static class UISkin
    {
        private const string ResourceDir = "UI/";

        /// <summary>
        /// 控件名 → 风格。没进表的 <see cref="Button"/> 一律按 <see cref="UIStyle.Secondary"/> 处理，
        /// 没进表的普通 <see cref="Image"/> 不动（全屏面板底、透明遮罩之类不该被套皮）。
        /// </summary>
        private static readonly Dictionary<string, UIStyle> StyleByName =
            new Dictionary<string, UIStyle>(StringComparer.Ordinal)
            {
                // 主行动
                { "StartButton", UIStyle.Primary },
                { "NextGroupButton", UIStyle.Primary },
                { "NextRunButton", UIStyle.Primary },
                { "SubmitButton", UIStyle.Primary },
                { "OfferItemTemplate", UIStyle.Primary },

                // 退一步
                { "LeaderboardButton", UIStyle.Secondary },
                { "CreditsButton", UIStyle.Secondary },
                { "QuitButton", UIStyle.Secondary },
                { "BackButton", UIStyle.Secondary },
                { "EndRunButton", UIStyle.Secondary },
                { "MenuButton", UIStyle.Secondary },

                // 底板
                { "HandItemTemplate", UIStyle.Card },
                { "Scoreboard", UIStyle.Panel },
                { "NameInput", UIStyle.Panel },
            };

        private static readonly Dictionary<UIStyle, string> SpriteByStyle =
            new Dictionary<UIStyle, string>
            {
                { UIStyle.Primary, "btn_primary" },
                { UIStyle.Secondary, "btn_secondary" },
                { UIStyle.Icon, "btn_icon" },
                { UIStyle.Card, "panel_bg" },
                { UIStyle.Panel, "panel_bg" },
            };

        // 木牌是暖橙、木条是米白，两者都偏亮，原来的白字会糊在底上；统一换成深褐系。
        private static readonly Color TextOnWood = new Color(0.29f, 0.17f, 0.05f, 1f);
        private static readonly Color TextOnLight = new Color(0.25f, 0.19f, 0.11f, 1f);
        private static readonly Color TextMuted = new Color(0.45f, 0.39f, 0.30f, 1f);

        private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        /// <summary>
        /// 给整棵 UI 树刷皮。重复调用是安全的（幂等），面板动态增删后可以再刷一遍。
        /// </summary>
        public static void Apply(Transform uiRoot)
        {
            if (uiRoot == null)
            {
                return;
            }

            // 先刷底板：底板会顺手改自己直属 Text 的颜色，而按钮的 Text 归按钮管，
            // 两者互不重叠（按钮的字是按钮的子节点，不是底板的直属子节点）。
            foreach (Image image in uiRoot.GetComponentsInChildren<Image>(true))
            {
                if (image.GetComponent<Button>() != null)
                {
                    continue;
                }

                UIStyle style;
                if (StyleByName.TryGetValue(image.gameObject.name, out style))
                {
                    ApplyPanel(image, style);
                }
            }

            foreach (Button button in uiRoot.GetComponentsInChildren<Button>(true))
            {
                UIStyle style;
                if (!StyleByName.TryGetValue(button.gameObject.name, out style))
                {
                    style = UIStyle.Secondary;
                }
                ApplyButton(button, style);
            }

            foreach (InputField input in uiRoot.GetComponentsInChildren<InputField>(true))
            {
                ApplyInputField(input);
            }
        }

        /// <summary>给单个按钮套皮。运行时现建的按钮（如触屏工具条）直接调这个。</summary>
        public static void ApplyButton(Button button, UIStyle style)
        {
            if (button == null)
            {
                return;
            }

            // 点击音也在套皮这个唯一入口统一补挂：静态树、运行时工具条、待克隆的列表模板都会路过这里
            if (button.GetComponent<UIButtonSound>() == null)
            {
                button.gameObject.AddComponent<UIButtonSound>();
            }

            var image = button.GetComponent<Image>();
            if (image != null && ApplySprite(image, style))
            {
                button.targetGraphic = image;
            }

            Color textColor = TextColor(style);
            foreach (Text text in button.GetComponentsInChildren<Text>(true))
            {
                text.color = textColor;
            }
        }

        /// <summary>给一块底板套皮，并把它**直属**的文字改成配得上的深色。</summary>
        public static void ApplyPanel(Image image, UIStyle style)
        {
            if (image == null || !ApplySprite(image, style))
            {
                return;
            }

            Color textColor = TextColor(style);
            foreach (Transform child in image.transform)
            {
                var text = child.GetComponent<Text>();
                if (text != null)
                {
                    text.color = textColor;
                }
            }
        }

        /// <summary>取一张已经切好九宫格的图。资源缺失时返回 null（不抛，缺图不该让游戏起不来）。</summary>
        public static Sprite Get(UIStyle style)
        {
            string key;
            if (!SpriteByStyle.TryGetValue(style, out key))
            {
                return null;
            }

            Sprite sprite;
            if (_sprites.TryGetValue(key, out sprite))
            {
                return sprite;
            }

            sprite = Resources.Load<Sprite>(ResourceDir + key);
            if (sprite == null)
            {
                Debug.LogWarning(
                    $"[UI] 缺少皮肤图 Resources/{ResourceDir}{key}.png，该控件保持原样式。");
            }
            _sprites[key] = sprite;
            return sprite;
        }

        private static bool ApplySprite(Image image, UIStyle style)
        {
            Sprite sprite = Get(style);
            if (sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            // Sliced：四角原样、四边只沿一个方向拉、中间双向拉。
            // 这套图的端头（木牌收口、方块的暗面倒角）全靠角和边保住，换成 Simple 就会整体压扁。
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            // 底色必须是纯白：Button 的状态过渡（highlighted / pressed / disabled）是拿这个颜色去乘的，
            // 留着原来的蓝色会把木头染成蓝木头。
            image.color = Color.white;
            return true;
        }

        private static void ApplyInputField(InputField input)
        {
            if (input.textComponent != null)
            {
                input.textComponent.color = TextOnLight;
            }
            if (input.placeholder != null)
            {
                var placeholder = input.placeholder as Text;
                if (placeholder != null)
                {
                    placeholder.color = TextMuted;
                }
            }
        }

        private static Color TextColor(UIStyle style)
        {
            switch (style)
            {
                case UIStyle.Primary:
                case UIStyle.Icon:
                    return TextOnWood;
                default:
                    return TextOnLight;
            }
        }
    }
}
