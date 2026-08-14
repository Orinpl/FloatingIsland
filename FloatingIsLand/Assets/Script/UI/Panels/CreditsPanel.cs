using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 开发者名单面板：从主界面进入的纯展示页，出口只有返回按钮。
    /// 名单是静态内容直接写在这里；布局由 BootSceneBuilder 生成，改人名不用重建场景。
    /// </summary>
    public sealed class CreditsPanel : UIPanel
    {
        public Text listText;
        public Button backButton;

        private const string CreditsContent =
            "北极  —  开发\n\n" +
            "Dawncxzz  —  TA\n\n" +
            "-H工H工H-  —  美术原画\n\n" +
            "星辉  —  地编\n\n" +
            "Windows98  —  音效";

        private void Awake()
        {
            if (listText != null)
            {
                listText.text = CreditsContent;
            }
        }
    }
}
