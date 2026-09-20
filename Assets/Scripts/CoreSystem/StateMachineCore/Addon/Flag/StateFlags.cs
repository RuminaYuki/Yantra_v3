using System;
using System.Collections.Generic;
using UnityEngine;

public class StateFlags : StateFlagsAccess
{
    [Serializable]
    private class FlagEntry
    {
        public FlagSO Flag;
        public bool InitialValue;

#if UNITY_EDITOR
        [SerializeField] private bool runtimeValue;

        public bool RuntimeValue
        {
            get => runtimeValue;
            set => runtimeValue = value;
        }
#endif
    }

    [SerializeField] private List<FlagEntry> flags = new();
    private readonly Dictionary<FlagSO, bool> values = new();

    private void Awake()
    {
        values.Clear();

        foreach (FlagEntry entry in flags)
        {
            if (entry.Flag == null) continue;

            if (values.ContainsKey(entry.Flag))
            {
                Debug.LogWarning($"Duplicate flag '{entry.Flag.name}' on {name}.", this);
                continue;
            }

            values.Add(entry.Flag, entry.InitialValue);

#if UNITY_EDITOR
            entry.RuntimeValue = entry.InitialValue;
#endif
        }
    }

    public override bool Get(FlagSO flag)
    {
        if (flag == null) return false;

#if UNITY_EDITOR
        foreach (FlagEntry entry in flags)
        {
            if (entry.Flag == flag) return entry.RuntimeValue;
        }
#endif

        return values.TryGetValue(flag, out bool value) && value;
    }

    public override void Set(FlagSO flag, bool value)
    {
        if (flag == null)
        {
            Debug.LogWarning("Cannot set a null flag.", this);
            return;
        }

        if (!values.ContainsKey(flag))
        {
            Debug.LogWarning($"Flag '{flag.name}' is not registered on {name}.", this);
            return;
        }

        values[flag] = value;

#if UNITY_EDITOR
        foreach (FlagEntry entry in flags)
        {
            if (entry.Flag != flag) continue;
            entry.RuntimeValue = value;
            break;
        }
#endif
    }

    public bool Contains(FlagSO flag)
    {
        return flag != null && values.ContainsKey(flag);
    }

    public bool this[FlagSO flag]
    {
        get => Get(flag);
        set => Set(flag, value);
    }

    public void ResetToInitialValues()
    {
        foreach (FlagEntry entry in flags)
        {
            if (entry.Flag == null) continue;
            values[entry.Flag] = entry.InitialValue;

#if UNITY_EDITOR
            entry.RuntimeValue = entry.InitialValue;
#endif
        }
    }
}
