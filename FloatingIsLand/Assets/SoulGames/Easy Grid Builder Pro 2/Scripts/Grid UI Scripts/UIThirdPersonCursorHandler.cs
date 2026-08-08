using UnityEngine;
using UnityEngine.InputSystem;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid UI/UI Third Person Cursor Handler", 1)]
    public class UIThirdPersonCursorHandler : MonoBehaviour
    {   
        [SerializeField] private RectTransform cursorPointerPanel;
        [SerializeField] private bool keepCursorLockedAtStart;
        [SerializeField] private InputAction cursorLockAndUnlockToggleAction;
        [SerializeField] private bool hideCursorPointerWhenCursorIsUnlocked;

        private void OnEnable()
        {
            cursorLockAndUnlockToggleAction.Enable();
            cursorLockAndUnlockToggleAction.performed += OnCursorLockAndUnlockTogglePerformed;
        }

        private void Start()
        {
            Cursor.lockState = keepCursorLockedAtStart ? CursorLockMode.Locked : CursorLockMode.None;

            if (!hideCursorPointerWhenCursorIsUnlocked) return;
            if (cursorPointerPanel && cursorPointerPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup))
            {
                canvasGroup.alpha = Cursor.lockState == CursorLockMode.Locked ? 1f : 0f;
            }
        }

        private void OnDisable()
        {
            cursorLockAndUnlockToggleAction.Disable();
        }
        
        private void OnCursorLockAndUnlockTogglePerformed(InputAction.CallbackContext context)
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.None ? CursorLockMode.Locked : CursorLockMode.None;
            
            if (!hideCursorPointerWhenCursorIsUnlocked) return;
            if (cursorPointerPanel && cursorPointerPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup))
            {
                canvasGroup.alpha = Cursor.lockState == CursorLockMode.Locked ? 1f : 0f;
            }
        }
    }
}