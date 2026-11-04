using System;
using SDFcl.GamePlay.Interactable;
using UnityEngine;

public class AnimInteractAddition : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] string AnimationID = string.Empty;

    Iinteractor _interactor;
    GameObject rootPlayer;
    PlayerInteract playerInteract;

    // The anim-end event this object is listening to. Every item listens to the same shared
    // event on the player's arm, so it must always be removed again, otherwise a destroyed
    // item stays subscribed and breaks the next pickup.
    SerializableAction subscribedAction;

    bool IsPlayAnimation = false;
    bool cancelNextFrame = false;

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        _interactor.OnInteract += HandleInteract;
        _interactor.AddCanCancelInteractModifier(CanCancel);
        _interactor.AddCanInteractModifier(CanInteract);
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        _interactor.RemoveCanCancelInteractModifier(CanCancel);
        _interactor.RemoveCanInteractModifier(CanInteract);

        Unsubscribe();

        // Disabled/destroyed mid-interaction (e.g. PickUpAddition destroys the item before
        // the anim end event fires). Nothing would cancel it any more, so release the player now.
        bool wasActive = IsPlayAnimation || cancelNextFrame;
        IsPlayAnimation = false;
        cancelNextFrame = false;
        if (wasActive && rootPlayer != null)
            _interactor.CancelInteraction(rootPlayer, true);
    }

    private void Update()
    {
        if (cancelNextFrame)
        {
            cancelNextFrame = false;
            if (rootPlayer != null) _interactor.CancelInteraction(rootPlayer, true);
            return;
        }

        if (!IsPlayAnimation) return;

        // Keeps asking until the animation is really playing. PlayAnimation returns null
        // while it is already playing or fading in, which is ignored here.
        Subscribe(playerInteract.PlayAnimation(AnimationID));
    }

    private bool CanInteract(GameObject rootPlayer)
    {
        if (IsPlayAnimation) return false;

        // Block while the player is already playing this same animation (e.g. from another item)
        PlayerInteract pi = rootPlayer != null ? rootPlayer.GetComponentInChildren<PlayerInteract>() : null;
        return pi == null || pi.CanPlayAnimation(AnimationID);
    }

    // Cancel only waits for our own animation; the anim-state check is left out on purpose
    // because the end event fires while the state is still playing.
    private bool CanCancel(GameObject rootPlayer)
    {
        return !IsPlayAnimation;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        rootPlayer = rootplayer;

        playerInteract = rootPlayer.GetComponentInChildren<PlayerInteract>();
        SerializableAction action = playerInteract.PlayAnimation(AnimationID);
        if (action == null)
        {
            // No animation started, so no end event will ever cancel this interaction.
            // Cancel next frame: we are still inside base.Interact, which sets
            // SetIsInterctable(true) after this and would overwrite a cancel made now.
            cancelNextFrame = true;
            return;
        }

        Subscribe(action);
        IsPlayAnimation = true;
    }

    private void HandleEndInteract()
    {
        Unsubscribe(); // stop listening before anything else runs
        IsPlayAnimation = false;
        _interactor.CancelInteraction(rootPlayer);
    }

    private void Subscribe(SerializableAction action)
    {
        if (action == null || action == subscribedAction) return;

        Unsubscribe();
        subscribedAction = action;
        subscribedAction.AddListener(HandleEndInteract);
    }

    private void Unsubscribe()
    {
        if (subscribedAction == null) return;

        subscribedAction.RemoveListener(HandleEndInteract);
        subscribedAction = null;
    }
}
