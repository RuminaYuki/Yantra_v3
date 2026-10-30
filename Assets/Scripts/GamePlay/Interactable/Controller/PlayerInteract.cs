using System;
using System.Linq;
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
        [SerializeField] InteracAnimation[] interacsAnimation;

        private IbaseInteractor activeInteraction;
        private InputAction subscribedAction;

        private bool isInteractable = false;

        #region System
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
        #endregion

        #region Logic Interact
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
                if (!activeInteraction.CanInteract(rootPlayer)) return;

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
            if (!IsAlive(targetInteractable) || !targetInteractable.CanInteract(rootPlayer))
            {
                return;
            }

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
        #endregion

        #region Animation Interact
        public SerializableAction PlayAnimation(string _ID)
        {
            for (int i = 0; i < interacsAnimation.Count(); i++)
            {
                if (interacsAnimation[i].ID != _ID) continue;

                Animator animator = interacsAnimation[i].Animator;
                int layer = interacsAnimation[i].indexLayer;
                string stateName = interacsAnimation[i].stateName;

                // Already playing, or already fading into it: don't restart the crossfade.
                // Restarting it every frame kept the old state (e.g. the gun clip) blending
                // and firing its animation events.
                bool playing = animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName);
                bool fadingIn = animator.IsInTransition(layer) &&
                                animator.GetNextAnimatorStateInfo(layer).IsName(stateName);
                if (playing || fadingIn)
                    return null;

                animator.CrossFade(stateName, interacsAnimation[i].crossfade, layer);

                return interacsAnimation[i].Dispat.
                    GetEvent(interacsAnimation[i].EventID);
            }
            return null;
        }


        #endregion

        #region API
        public bool GetIsInterctable => isInteractable;
        public void SetIsInterctable(bool value) 
        {
            isInteractable = value;

            if (!isInteractable) ResetInteractionState();
        }
        #endregion
    }

    [Serializable]
    public struct InteracAnimation
    {
        public string ID;
        public string EventID;
        public string stateName;
        public int indexLayer;
        public float crossfade;
        public AnimEventDispatcher Dispat;
        public Animator Animator;
        private Action action;
    }
}
