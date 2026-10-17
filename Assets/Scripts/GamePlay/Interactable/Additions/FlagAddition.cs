using System;
using System.Collections.Generic;
using NUnit.Framework;
using SDFcl.GamePlay.Interactable;
using UnityEngine;

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
            interactor.OnInteract += HandleInteract;
        }
    }

    private void OnDisable()
    {
        if (interactor != null)
        {
            interactor.OnInteract -= HandleInteract;
        }
    }

    public void HandleInteract(GameObject rootplayer)
    {
        if (interactor == null) return;

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

    //ถ้าอยากให้บังคับใส่ค่าแม้จะไม่มี flagSO อยู่ใน state flags ก็ตาม
    [Tooltip("If true, the flag will be added even if it doesn't exist in the state flags.")]
    public bool forceAdd;

    public FlagAdditionData(FlagSO flagSO, bool value, bool forceAdd = false)
    {
        this.flagSO = flagSO;
        this.value = value;
        this.forceAdd = forceAdd;
    }
}