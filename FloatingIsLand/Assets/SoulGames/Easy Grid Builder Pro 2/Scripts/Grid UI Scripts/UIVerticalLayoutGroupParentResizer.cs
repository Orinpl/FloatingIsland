using UnityEngine;
using UnityEngine.UI;

namespace SoulGames.EasyGridBuilderPro
{
    public class UIVerticalLayoutGroupParentResizer : MonoBehaviour
    {
        [SerializeField] private RectTransform parentPanel;  
        [SerializeField] private VerticalLayoutGroup verticalLayoutGroup;
        [SerializeField] private float childHeight = 100f;
        [SerializeField] private bool updateEveryFrame = false;

        private void Start()
        {
            AdjustPanelHeight();
        }

        private void OnEnable()
        {
            AdjustPanelHeight();
        }

        private void Update()
        {
            if (updateEveryFrame) AdjustPanelHeight();
        }

        private void AdjustPanelHeight()
        {
            int activeChildCount = 0;

            // Count only active (enabled) child objects
            foreach (Transform child in parentPanel)
            {
                if (child.gameObject.activeSelf) activeChildCount++;
            }

            if (activeChildCount == 0) return;

            // Calculate total height based on active children, spacing, and padding
            float totalHeight = (activeChildCount * childHeight) + ((activeChildCount - 1) * verticalLayoutGroup.spacing) + verticalLayoutGroup.padding.top + verticalLayoutGroup.padding.bottom;
            parentPanel.sizeDelta = new Vector2(parentPanel.sizeDelta.x, totalHeight);
        }
    }
}