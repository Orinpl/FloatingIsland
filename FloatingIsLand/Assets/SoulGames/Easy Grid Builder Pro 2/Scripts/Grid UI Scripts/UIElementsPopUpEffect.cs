using UnityEngine;
using UnityEngine.EventSystems;

namespace SoulGames.EasyGridBuilderPro
{
    public class UIElementsPopUpEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform buttonRectTransform;
        [SerializeField] private float scaleFactor = 1.2f;
        [SerializeField] private float animationSpeed = 0.1f;

        private Vector3 originalScale;

        private void Awake()
        {
            if (buttonRectTransform == null)buttonRectTransform = GetComponent<RectTransform>();
            originalScale = buttonRectTransform.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            StopAllCoroutines();
            StartCoroutine(ScaleUIElement(originalScale * scaleFactor));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StopAllCoroutines();
            StartCoroutine(ScaleUIElement(originalScale));
        }

        private System.Collections.IEnumerator ScaleUIElement(Vector3 targetScale)
        {
            float time = 0f;
            Vector3 startScale = buttonRectTransform.localScale;

            while (time < animationSpeed)
            {
                buttonRectTransform.localScale = Vector3.Lerp(startScale, targetScale, time / animationSpeed);
                time += Time.deltaTime;
                yield return null;
            }

            buttonRectTransform.localScale = targetScale;
        }
    }
}