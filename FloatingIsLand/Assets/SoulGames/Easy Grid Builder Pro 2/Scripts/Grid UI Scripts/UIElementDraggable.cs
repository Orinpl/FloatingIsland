using UnityEngine;
using UnityEngine.EventSystems;

namespace SoulGames.EasyGridBuilderPro
{
    public class UIElementDraggable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private Canvas canvas;
        private RectTransform rectTransform;
        private Vector2 originalPosition;
        private bool isDragging;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (!canvas) canvas = GetComponentInParent<Canvas>();
            originalPosition = rectTransform.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isDragging = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isDragging = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }

        public void ResetPosition()
        {
            rectTransform.anchoredPosition = originalPosition;
        }
    }
}