using System;
using UnityEngine;

public abstract class StateFlagsAccess : MonoBehaviour
{
    // Raised only when a flag's value actually changes.
    // Subscribe in OnEnable (+=) and unsubscribe in OnDisable (-=).
    public event Action<FlagSO, bool> FlagChanged;

    // C# only lets the declaring class invoke an event, so subclasses raise it through this.
    protected void RaiseFlagChanged(FlagSO flag, bool value)
    {
        FlagChanged?.Invoke(flag, value);
    }

    public abstract bool Get(FlagSO flag);
    public abstract void Set(FlagSO flag, bool value);
}
