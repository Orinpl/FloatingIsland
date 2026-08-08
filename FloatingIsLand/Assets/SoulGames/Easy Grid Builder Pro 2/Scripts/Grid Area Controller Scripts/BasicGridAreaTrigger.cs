using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class BasicGridAreaTrigger : MonoBehaviour
    {
        public static event OnGhostObjectEnterBasicGridAreaDisablerDelegate OnGhostObjectEnterBasicGridAreaDisabler;
        public delegate void OnGhostObjectEnterBasicGridAreaDisablerDelegate();

        public static event OnGhostObjectExitBasicGridAreaDisablerDelegate OnGhostObjectExitBasicGridAreaDisabler;
        public delegate void OnGhostObjectExitBasicGridAreaDisablerDelegate();

        public static event OnGhostObjectEnterBasicGridAreaEnablerDelegate OnGhostObjectEnterBasicGridAreaEnabler;
        public delegate void OnGhostObjectEnterBasicGridAreaEnablerDelegate();

        public static event OnGhostObjectExitBasicGridAreaEnablerDelegate OnGhostObjectExitBasicGridAreaEnabler;
        public delegate void OnGhostObjectExitBasicGridAreaEnablerDelegate();

        private BasicGridAreaTriggerInvokerType triggerInvokerType;
        
        public void InvokeBasicGridAreaTrigger(BasicGridAreaTriggerInvokerType triggerInvokerType)
        {
            this.triggerInvokerType = triggerInvokerType;

            Rigidbody rb;
            if (!TryGetComponent<Rigidbody>(out Rigidbody rigidBody)) rb = gameObject.AddComponent<Rigidbody>();
            else rb = rigidBody;
            rb.isKinematic = enabled;
        }

        private void OnDestroy()
        {
            OnGhostObjectExitBasicGridAreaDisabler?.Invoke();
            OnGhostObjectExitBasicGridAreaEnabler?.Invoke();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.TryGetComponent<BasicGridAreaDisabler>(out BasicGridAreaDisabler basicGridAreaDisabler))
            {
                if (!IsPossibleToDisabledByBasicGridAreaDisabler(basicGridAreaDisabler)) return;
                OnGhostObjectEnterBasicGridAreaDisabler?.Invoke();
            }
            
            if (other.gameObject.TryGetComponent<BasicGridAreaEnabler>(out BasicGridAreaEnabler basicGridAreaEnabler))
            {
                if (!IsPossibleToEnabledByBasicGridAreaDisabler(basicGridAreaEnabler)) return;
                OnGhostObjectEnterBasicGridAreaEnabler?.Invoke();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.gameObject.TryGetComponent<BasicGridAreaDisabler>(out BasicGridAreaDisabler basicGridAreaDisabler))
            {
                if (!IsPossibleToDisabledByBasicGridAreaDisabler(basicGridAreaDisabler)) return;
                OnGhostObjectEnterBasicGridAreaDisabler?.Invoke();
            }

            if (other.gameObject.TryGetComponent<BasicGridAreaEnabler>(out BasicGridAreaEnabler basicGridAreaEnabler))
            {
                if (!IsPossibleToEnabledByBasicGridAreaDisabler(basicGridAreaEnabler)) return;
                OnGhostObjectEnterBasicGridAreaEnabler?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.TryGetComponent<BasicGridAreaDisabler>(out BasicGridAreaDisabler basicGridAreaDisabler)) 
            {
                if (!IsPossibleToDisabledByBasicGridAreaDisabler(basicGridAreaDisabler)) return;
                OnGhostObjectExitBasicGridAreaDisabler?.Invoke();
            }

            if (other.gameObject.TryGetComponent<BasicGridAreaEnabler>(out BasicGridAreaEnabler basicGridAreaEnabler))
            {
                if (!IsPossibleToEnabledByBasicGridAreaDisabler(basicGridAreaEnabler)) return;
                OnGhostObjectExitBasicGridAreaEnabler?.Invoke();
            }
        }

        private bool IsPossibleToDisabledByBasicGridAreaDisabler(BasicGridAreaDisabler basicGridAreaDisabler)
        {
            bool isValid = true;
            switch (triggerInvokerType)
            {
                case BasicGridAreaTriggerInvokerType.GridObject: if (!basicGridAreaDisabler.GetIsBlockGridObjects()) isValid = false; break;
                case BasicGridAreaTriggerInvokerType.EdgeObject: if (!basicGridAreaDisabler.GetIsBlockEdgeObjects()) isValid = false; break;
                case BasicGridAreaTriggerInvokerType.CornerObject: if (!basicGridAreaDisabler.GetIsBlockCornerObjects()) isValid = false; break;
                case BasicGridAreaTriggerInvokerType.FreeObject: if (!basicGridAreaDisabler.GetIsBlockFreeObjects()) isValid = false; break;
            }
            return isValid;
        }

        private bool IsPossibleToEnabledByBasicGridAreaDisabler(BasicGridAreaEnabler basicGridAreaEnabler)
        {
            bool isValid = true;
            switch (triggerInvokerType)
            {
                case BasicGridAreaTriggerInvokerType.GridObject: if (!basicGridAreaEnabler.GetIsEnableGridObjects()) isValid = false; break;
                case BasicGridAreaTriggerInvokerType.EdgeObject: if (!basicGridAreaEnabler.GetIsEnableEdgeObjects()) isValid = false; break;
                case BasicGridAreaTriggerInvokerType.CornerObject: if (!basicGridAreaEnabler.GetIsEnableCornerObjects()) isValid = false; break;
                case BasicGridAreaTriggerInvokerType.FreeObject: if (!basicGridAreaEnabler.GetIsEnableFreeObjects()) isValid = false; break;
            }
            return isValid;
        }
    }
}