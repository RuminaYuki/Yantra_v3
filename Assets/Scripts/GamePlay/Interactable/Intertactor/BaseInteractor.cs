using System;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Cinemachine.CinemachineFreeLookModifier;

namespace SDFcl.GamePlay.Interactable
{
    public abstract class BaseInteractor : MonoBehaviour, IbaseInteractor , Iinteractor
    {
        [Header("Highlight and Focus Objects")]
        [SerializeField] private GameObject highlightObject;
        [SerializeField] private GameObject focusObject;

        [Header("Interaction Settings")]
        [Tooltip("If true, the player can interact with this object.")]
        [SerializeField] protected bool canInteract = true;
        [Tooltip("If true, the highlight will be hidden when canInteract is false.")]
        [SerializeField] protected bool hideInteract = false;
        [Tooltip("If true, ")]
        [SerializeField] private bool holdInteract = false;

        private List<Func<bool>> canInteractFuncs = new();
        private List<Func<bool>> canCancelInteractFuncs = new();

        //if hideInteract is true, CanInteract will always return the value of canInteract, otherwise it will return the value of false
        public bool CanInteract 
        {
            get
            {
                foreach (var modifier in canInteractFuncs)
                {
                    if (!modifier())
                    {
                        return false;
                    }
                }
                return hideInteract? canInteract : true; 
            }
        }
        public bool HoldInteract => holdInteract;

        public Action<GameObject> OnInteract { get; set; }
        public Action<GameObject> OnEndInteract { get; set; }

        private void Awake()
        {
            //Disable this script if there is no highlight or focus object assigned
            if (highlightObject == null && focusObject == null)
            {
                this.enabled = false;
                return;
            }

            highlightObject.SetActive(false);
            focusObject.SetActive(false);
        }

        public virtual bool Interact(GameObject rootplayer, bool force = false)
        {
            if (!canInteract)
            {
                Debug.LogWarning($"Interactable {this.gameObject.name} is not interactable.");
                return false;
            }

            OnInteract?.Invoke(rootplayer);

            PlayerInteract playerInteract = rootplayer.GetComponentInChildren<PlayerInteract>();
            playerInteract.SetIsInterctable(true);

            return true;
            //Debug.Log($"Interact input detected{this.gameObject.name}");
        }

        public virtual void OnFocus()
        {
            if (!canInteract) return;
            focusObject.SetActive(true);
            highlightObject.SetActive(false);
        }

        public virtual void OnLoseFocus()
        {
            focusObject.SetActive(false);
            highlightObject.SetActive(true);
        }

        public virtual void ShowHighlight()
        {
            highlightObject.SetActive(true);
            focusObject.SetActive(false);
        }

        public virtual void HideHighlight()
        {
            highlightObject.SetActive(false);
            focusObject.SetActive(false);
        }

        public virtual bool CancelInteraction(GameObject rootplayer, bool force = false)
        {
            if (!force)
            {
                foreach (var modifier in canCancelInteractFuncs)
                {
                    if (!modifier())
                    {
                        return false;
                    }
                }
            }
            OnEndInteract?.Invoke(rootplayer);

            PlayerInteract playerInteract = rootplayer.GetComponentInChildren<PlayerInteract>();
            playerInteract.SetIsInterctable(false);

            return true;
        }

        //API set CanInteract
        public void SetCanInteract(bool value)
        {
            canInteract = value;
        }

        public void AddCanInteractModifier(Func<bool> modifier)
        {
            canInteractFuncs.Add(modifier);
        }
        public void RemoveCanInteractModifier(Func<bool> modifier)
        {
            canInteractFuncs.Remove(modifier);
        }
        public void AddCanCancelInteractModifier(Func<bool> modifier)
        {
            canCancelInteractFuncs.Add(modifier);
        }
        public void RemoveCanCancelInteractModifier(Func<bool> modifier)
        {
            canCancelInteractFuncs.Remove(modifier);
        }
    }

    public interface IbaseInteractor
    {
        bool CanInteract { get; }
        bool HoldInteract {  get; }

        //Command the object to perform its interaction logic
        bool Interact(GameObject rootplayer, bool force = false);

        //Command the object to show Focus when in Camera forward
        void OnFocus();
        void OnLoseFocus();

        //Command the object to show Highlight when in Highlight range
        void ShowHighlight();
        void HideHighlight();

        bool CancelInteraction(GameObject rootplayer, bool force = false);
    }

    public interface Iinteractor
    {
        void AddCanInteractModifier(Func<bool> modifier);
        void RemoveCanInteractModifier(Func<bool> modifier);
        void AddCanCancelInteractModifier(Func<bool> modifier);
        void RemoveCanCancelInteractModifier(Func<bool> modifier);
        public Action<GameObject> OnInteract { get; set; }
        public Action<GameObject> OnEndInteract { get; set; }

        bool CancelInteraction(GameObject rootplayer, bool force = false);
    }
}