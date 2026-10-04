using System;
using SDFcl.GamePlay.Interactable;
using UnityEngine;

public class AnimInteractAddition : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] string AnimationID = string.Empty;

    Iinteractor _interactor;
    GameObject rootPlayer;
    SerializableAction serializableAction;
    PlayerInteract playerInteract;

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

        if (serializableAction != null )
        {
            serializableAction.AddListener(HandleEndInteract);
        }
    }
    

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        _interactor.RemoveCanCancelInteractModifier(CanInteract);
        _interactor.RemoveCanInteractModifier(CanInteract);

        if (serializableAction != null)
        {
            serializableAction.RemoveListener(HandleEndInteract);
        }
    }

    private bool CanInteract()
    {
        return !IsPlayAnimation;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        rootPlayer = rootplayer;

        playerInteract = rootPlayer.GetComponentInChildren<PlayerInteract>();
        serializableAction = playerInteract.PlayAnimation(AnimationID);
        if (serializableAction != null )
        {
            serializableAction.AddListener(HandleEndInteract);
            IsPlayAnimation = true;
        }
    }
    private void HandleEndInteract()
    {
        IsPlayAnimation = false;
        _interactor.CancelInteraction(rootPlayer);
    }
}
