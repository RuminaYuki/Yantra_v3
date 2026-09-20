using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FlagCDData
{
    public FlagSO flag;
    public float countdownTime;
    public float remainingTime;
    public Coroutine countdownCoroutine;

    public FlagCDData(FlagSO flag, float countdownTime)
    {
        this.flag = flag;
        this.countdownTime = countdownTime;
    }
}

public class FlagCountdown : MonoBehaviour
{
    [SerializeField] private List<FlagCDData> flagCountdowns = new();
    [SerializeField] private StateFlags stateFlags;

    private void Awake()
    {
        if (stateFlags == null)
            stateFlags = GetComponent<StateFlags>();
    }

    //function to set a flag with a countdown timer,
    //if the flag is already set, it will reset the countdown timer if the new countdown time is greater than the current countdown time
    public void SetFlagCountdown(FlagSO flag, float countdownTime, bool flagValue = true)
    {
        foreach (var flagData in flagCountdowns)
        {
            if (flagData.flag == flag)
            {
                if (flagData.remainingTime < countdownTime)
                SetStartCountdown(flagData, countdownTime, flagValue);
                return;
            }
        }
        
        FlagCDData newFlagData = new FlagCDData(flag, countdownTime);
        flagCountdowns.Add(newFlagData);
        Debug.Log($"SetFlagCountdown: {flag.name}, countdownTime: {countdownTime}, flagValue: {flagValue}");
        SetStartCountdown(newFlagData, countdownTime, flagValue);
    }
    
    public float GetRemainingTime(FlagSO flag)
    {
        foreach (var flagData in flagCountdowns)
        {
            if (flagData.flag == flag)
                return flagData.remainingTime;
        }
        return 0f;
    }

    public bool Contains(FlagSO flag)
    {
        foreach (var flagData in flagCountdowns)
        {
            if (flagData.flag == flag)
                return true;
        }
        return false;
    }

    private void SetStartCountdown(FlagCDData flagData, float countdownTime, bool flagValue)
    {
        stateFlags.Set(flagData.flag, flagValue);
        if (flagData.countdownCoroutine != null)
        {
            StopCoroutine(flagData.countdownCoroutine);
        }
        flagData.countdownTime = countdownTime;
        flagData.remainingTime = countdownTime;
        flagData.countdownCoroutine = StartCoroutine(CountdownCoroutine(flagData, !flagValue));
    }

    private IEnumerator CountdownCoroutine(FlagCDData flagData, bool flagValue)
    {
        while (flagData.remainingTime > 0)
        {
            flagData.remainingTime -= Time.deltaTime;
            //Debug.Log($"CountdownCoroutine: {flagData.flag.name}, remainingTime: {flagData.remainingTime}");
            yield return null;
        }
        stateFlags.Set(flagData.flag, flagValue);
        flagCountdowns.Remove(flagData);
    }

    private void OnDestroy()
    {
        foreach (var flagData in flagCountdowns)
        {
            if (flagData.countdownCoroutine != null)
            {
                StopCoroutine(flagData.countdownCoroutine);
            }
        }
        flagCountdowns.Clear();
    }

    public void StopCountdown(FlagSO flag)
    {
        for (int i = 0; i < flagCountdowns.Count; i++)
        {
            if (flagCountdowns[i].flag == flag)
            {
                if (flagCountdowns[i].countdownCoroutine != null)
                {
                    flagCountdowns[i].remainingTime = 0;
                    //StopCoroutine(flagCountdowns[i].countdownCoroutine);
                }
                //flagCountdowns.RemoveAt(i);
                return;
            }
        }
    }

    public void ResetAllCountdown()
    {
        foreach (FlagCDData flagData in flagCountdowns)
        {
            if (flagData.countdownCoroutine != null)
            {
                StopCoroutine(flagData.countdownCoroutine);
                flagData.countdownCoroutine = null;
            }

            flagData.remainingTime = 0f;
        }

        flagCountdowns.Clear();
    }
}
