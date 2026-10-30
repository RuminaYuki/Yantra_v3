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

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        _interactor.OnInteract += HandleInteract;
        _interactor.AddCanCancelInteractModifier(CanInteract);
        _interactor.AddCanInteractModifier(CanInteract);
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        _interactor.RemoveCanCancelInteractModifier(CanInteract);
        _interactor.RemoveCanInteractModifier(CanInteract);

        Unsubscribe();
    }

    private void Update()
    {
        if (!IsPlayAnimation) return;

        // Keeps asking until the animation is really playing. PlayAnimation returns null
        // while it is already playing or fading in, which is ignored here.
        Subscribe(playerInteract.PlayAnimation(AnimationID));
    }

    private bool CanInteract(GameObject rootPlayer)
    {
        return !IsPlayAnimation;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        rootPlayer = rootplayer;

        playerInteract = rootPlayer.GetComponentInChildren<PlayerInteract>();
        SerializableAction action = playerInteract.PlayAnimation(AnimationID);
        if (action == null) return;

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
