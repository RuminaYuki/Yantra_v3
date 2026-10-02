using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SDFcl.GamePlay.Interactable
{
    [RequireComponent(typeof(CenterRayInteract))]
    public class PlayerInteract : MonoBehaviour
    {
        [SerializeField] private InputActionReference inputAction;
        [SerializeField] private CenterRayInteract rayInteract;
        [SerializeField] private GameObject rootPlayer;

        [SerializeField] private bool isHoldingInteraction = false;

        public event Action<bool> OnInteraction;
        private IbaseInteractor activeInteraction;
        private InputAction subscribedAction;

        private void Awake()
        {
            rayInteract = GetComponent<CenterRayInteract>();
            if (inputAction == null)
            {
                Debug.LogError("Input Action Reference is not assigned.");
                enabled = false;
                rayInteract.enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            SubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            ResetInteractionState();
        }

        private void OnDestroy()
        {
            UnsubscribeInput();
        }

        private void SubscribeInput()
        {
            if (inputAction == null || inputAction.action == null)
                return;

            if (subscribedAction == inputAction.action)
                return;

            UnsubscribeInput();
            subscribedAction = inputAction.action;
            subscribedAction.started += HandleInteractInput;
        }

        private void UnsubscribeInput()
        {
            if (subscribedAction == null)
                return;

            subscribedAction.started -= HandleInteractInput;
            subscribedAction = null;
        }

        private void HandleInteractInput(InputAction.CallbackContext context)
        {
            if (!context.started)
                return;

            if (rayInteract == null || rootPlayer == null)
                return;

            if (isHoldingInteraction)
            {
                isHoldingInteraction = false;
                rayInteract.SetInteractEnabled(true);

                if (IsAlive(activeInteraction))
                {
                    activeInteraction.CancelInteraction(rootPlayer);
                }

                activeInteraction = null;

                return;
            }

            IbaseInteractor targetInteractable = rayInteract.CurrentInteractable;
            if (!IsAlive(targetInteractable) || !targetInteractable.CanInteract)
            {
                OnInteraction?.Invoke(false);
                return;
            }

            OnInteraction?.Invoke(true);

            if (!targetInteractable.HoldInteract)
            {
                targetInteractable.Interact(rootPlayer);
                return;
            }

            if (targetInteractable.Interact(rootPlayer))
            {
                activeInteraction = targetInteractable;
                isHoldingInteraction = true;
                rayInteract.SetInteractEnabled(false);
                rayInteract.StopHighlightingAll();
            }
        }

        private void ResetInteractionState()
        {
            isHoldingInteraction = false;
            activeInteraction = null;

            if (rayInteract != null)
                rayInteract.SetInteractEnabled(true);
        }

        private static bool IsAlive(IbaseInteractor interactable)
        {
            return interactable is MonoBehaviour target && target != null;
        }
    }
}
