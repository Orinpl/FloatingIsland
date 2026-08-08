using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoulGames.EasyGridBuilderPro
{
    public class UIElementsMaskMoveEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform movingRectTransform;
        [SerializeField] private Vector3 targetPositionOffset = new Vector3(100f, 100f, 0f);
        [SerializeField] private float animationSpeed = 0.1f;

        private Vector3 originalPosition;

        private void Awake()
        {
            if (movingRectTransform == null) movingRectTransform = GetComponent<RectTransform>();
            originalPosition = movingRectTransform.localPosition;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            StopAllCoroutines();
            StartCoroutine(MoveUIElement(originalPosition + targetPositionOffset, true));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StopAllCoroutines();
            StartCoroutine(MoveUIElement(originalPosition, false));
        }

        private System.Collections.IEnumerator MoveUIElement(Vector3 targetPosition, bool isEntering)
        {
            float time = 0f;
            Vector3 startPosition = movingRectTransform.localPosition;

            while (time < animationSpeed)
            {
                movingRectTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, time / animationSpeed);
                time += Time.deltaTime;

                yield return null;
            }

            // Ensure the final position is set exactly
            movingRectTransform.localPosition = targetPosition;
        }
    }
}