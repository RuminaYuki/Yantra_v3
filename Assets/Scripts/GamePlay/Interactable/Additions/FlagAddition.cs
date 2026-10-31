using System;
using System.Collections.Generic;
using SDFcl.GamePlay.Interactable;
using UnityEngine;

// Sets player StateFlags when this object is interacted with.
// Entries with "Lock When Reached" block the interaction once the flag already holds the value.
// The lock reads the flag itself, so it survives save/load as long as the flag is listed
// in FlagSaveable on the player.
public class FlagAddition : MonoBehaviour
{
    [SerializeField] List<FlagAdditionData> ListFlag;

    Iinteractor interactor;

    private void Awake()
    {
        interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        if (interactor != null)
        {
            interactor.OnEndInteract += HandleEndInteract;
            interactor.AddCanInteractModifier(CanInteract);
        }
    }

    private void OnDisable()
    {
        if (interactor != null)
        {
            interactor.OnEndInteract -= HandleEndInteract;
            interactor.RemoveCanInteractModifier(CanInteract);
        }
    }

    // False when any locking entry already has its target value.
    private bool CanInteract(GameObject rootplayer)
    {
        if (rootplayer == null) return true;

        StateFlags stateFlags = rootplayer.GetComponentInChildren<StateFlags>();
        if (stateFlags == null) return true;

        foreach (FlagAdditionData flagAddition in ListFlag)
        {
            if (!flagAddition.lockWhenReached || flagAddition.flagSO == null) continue;
            if (!stateFlags.Contains(flagAddition.flagSO)) continue;

            if (stateFlags.Get(flagAddition.flagSO) == flagAddition.value) return false;
        }
        return true;
    }

    public void HandleEndInteract(GameObject rootplayer)
    {
        if (rootplayer == null) return;

        StateFlags stateFlags = rootplayer.GetComponentInChildren<StateFlags>();

        if (stateFlags == null) return;

        foreach (FlagAdditionData flagAddition in ListFlag)
        {
            if (flagAddition.flagSO == null) continue;
            if (flagAddition.forceAdd || stateFlags.Contains(flagAddition.flagSO))
            {
                stateFlags.Set(flagAddition.flagSO, flagAddition.value);
            }
        }
    }
}

[Serializable]
public struct FlagAdditionData
{
    public FlagSO flagSO;
    public bool value;

    // Set the flag even when it is not registered in the player's StateFlags.
    [Tooltip("If true, the flag will be added even if it doesn't exist in the state flags.")]
    public bool forceAdd;

    [Tooltip("If true, this object can no longer be interacted with once the flag equals Value.\n" +
             "Add the flag to FlagSaveable on the player so the lock survives save/load.")]
    public bool lockWhenReached;

    public FlagAdditionData(FlagSO flagSO, bool value, bool forceAdd = false, bool lockWhenReached = false)
    {
        this.flagSO = flagSO;
        this.value = value;
        this.forceAdd = forceAdd;
        this.lockWhenReached = lockWhenReached;
    }
}
