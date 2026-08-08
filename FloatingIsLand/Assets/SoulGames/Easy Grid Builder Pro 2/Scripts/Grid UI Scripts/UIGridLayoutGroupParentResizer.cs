using UnityEngine;
using UnityEngine.UI;

namespace SoulGames.EasyGridBuilderPro
{
    public class UIGridLayoutGroupParentResizer : MonoBehaviour
    {
        public enum ResizeMode { Horizontal, Vertical, Both }

        [SerializeField] private RectTransform parentPanel;
        [SerializeField] private GridLayoutGroup gridLayoutGroup; 
        [SerializeField] private int maxRows = 1;                   // Maximum rows for grid layout
        [SerializeField] private int maxColumns = 1;                // Maximum columns for grid layout
        [SerializeField] private ResizeMode resizeMode = ResizeMode.Horizontal;
        [SerializeField] private bool updateEveryFrame = false;

        private void Start()
        {
            AdjustPanelSize();
        }

        private void OnEnable()
        {
            AdjustPanelSize();
        }

        private void Update()
        {
            if (updateEveryFrame) AdjustPanelSize();
        }

        private void AdjustPanelSize()
        {
            int activeChildCount = 0;

            // Count only active (enabled) child objects
            foreach (Transform child in parentPanel)
            {
                if (child.gameObject.activeSelf) activeChildCount++;
            }
            
            if (activeChildCount == 0) return;

            float newWidth = parentPanel.sizeDelta.x;
            float newHeight = parentPanel.sizeDelta.y;

            if (resizeMode == ResizeMode.Horizontal || resizeMode == ResizeMode.Both)
            {
                // Calculate columns based on the number of children and max rows
                int columns = Mathf.CeilToInt((float)activeChildCount / maxRows);

                // Calculate required width based on cell size, spacing, and padding
                newWidth = (columns * gridLayoutGroup.cellSize.x) + ((columns - 1) * gridLayoutGroup.spacing.x) + gridLayoutGroup.padding.left + gridLayoutGroup.padding.right;
            }

            if (resizeMode == ResizeMode.Vertical || resizeMode == ResizeMode.Both)
            {
                // Calculate rows based on the number of children and max columns
                int rows = Mathf.CeilToInt((float)activeChildCount / maxColumns);

                // Calculate required height based on cell size, spacing, and padding
                newHeight = (rows * gridLayoutGroup.cellSize.y) + ((rows - 1) * gridLayoutGroup.spacing.y) + gridLayoutGroup.padding.top + gridLayoutGroup.padding.bottom;
            }

            // Set the panel's size
            parentPanel.sizeDelta = new Vector2(newWidth, newHeight);
        }
    }
}