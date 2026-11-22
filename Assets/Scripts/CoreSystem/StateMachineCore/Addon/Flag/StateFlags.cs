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

    // FlagChanged (declared in StateFlagsAccess) is raised via Set, the indexer, ResetToInitialValues,
    // and in the Editor when RuntimeValue is ticked in the Inspector during Play (see OnValidate).

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

#if UNITY_EDITOR
    // Inspector edits write RuntimeValue directly (no Set call), so sync them into `values`
    // and raise FlagChanged here. Play Mode only — OnValidate also runs on load/recompile.
    private void OnValidate()
    {
        if (!Application.isPlaying) return;

        foreach (FlagEntry entry in flags)
        {
            if (entry.Flag == null || !values.TryGetValue(entry.Flag, out bool oldValue)) continue;
            if (oldValue == entry.RuntimeValue) continue;

            values[entry.Flag] = entry.RuntimeValue;
            RaiseFlagChanged(entry.Flag, entry.RuntimeValue);
        }
    }
#endif

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

        bool oldValue = Get(flag);   // Get, not values[flag]: in the Editor the Inspector value is the truth
        values[flag] = value;

#if UNITY_EDITOR
        foreach (FlagEntry entry in flags)
        {
            if (entry.Flag != flag) continue;
            entry.RuntimeValue = value;
            break;
        }
#endif

        if (oldValue != value) RaiseFlagChanged(flag, value);
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
            bool oldValue = Get(entry.Flag);
            values[entry.Flag] = entry.InitialValue;

#if UNITY_EDITOR
            entry.RuntimeValue = entry.InitialValue;
#endif

            if (oldValue != entry.InitialValue) RaiseFlagChanged(entry.Flag, entry.InitialValue);
        }
    }
}
