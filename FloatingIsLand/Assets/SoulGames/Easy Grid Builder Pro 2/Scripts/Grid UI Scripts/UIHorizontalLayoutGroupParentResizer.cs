using UnityEngine;
using UnityEngine.UI;

namespace SoulGames.EasyGridBuilderPro
{
    public class UIHorizontalLayoutGroupParentResizer : MonoBehaviour
    {
        [SerializeField] private RectTransform parentPanel;
        [SerializeField] private HorizontalLayoutGroup horizontalLayoutGroup;
        [SerializeField] private float childWidth = 100f;
        [SerializeField] private bool updateEveryFrame = false;

        private void Start()
        {
            AdjustPanelWidth();
        }

        private void OnEnable()
        {
            AdjustPanelWidth();
        }

        private void Update()
        {
            if (updateEveryFrame) AdjustPanelWidth();
        }

        private void AdjustPanelWidth()
        {
            int activeChildCount = 0;
            
            // Count only active (enabled) child objects
            foreach (Transform child in parentPanel)
            {
                if (child.gameObject.activeSelf) activeChildCount++;
            }

            if (activeChildCount == 0) return;

            // Calculate total width based on children, spacing, and padding
            float totalWidth = (activeChildCount * childWidth) + ((activeChildCount - 1) * horizontalLayoutGroup.spacing) + horizontalLayoutGroup.padding.left + horizontalLayoutGroup.padding.right;
            parentPanel.sizeDelta = new Vector2(totalWidth, parentPanel.sizeDelta.y);
        }
    }
}