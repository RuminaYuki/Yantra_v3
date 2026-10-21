using System;
using System.Collections.Generic;
using UnityEngine;

// Saves only the StateFlags you pick. Every other flag starts from its Initial Value as before.
// Put it on the player root (next to PlayerSaveable).
// Uses only the public Get / Set / Contains of StateFlags, so StateFlags itself is not changed.
//
// Runs a little earlier than other scripts so states that read flags in Start see the loaded values.
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class FlagSaveable : SaveableBehaviour<FlagSaveable.SaveData>
{
    [Serializable]
    public class Entry
    {
        public FlagSO flag;

        [Tooltip("Name stored in the save file. Filled from the flag asset name.\n" +
                 "Keep it the same after release, even if the asset is renamed.")]
        public string key;
    }

    [Serializable]
    public class SavedFlag
    {
        public string key;
        public bool value;
    }

    [Serializable]
    public class SaveData
    {
        public List<SavedFlag> flags = new List<SavedFlag>();
    }

    [Tooltip("Empty = found on this object or its children.")]
    [SerializeField] private StateFlags stateFlags;

    [Tooltip("Only these flags are saved.")]
    [SerializeField] private List<Entry> flagsToSave = new List<Entry>();

    protected override string DefaultKey => "stateFlags";

    private void Awake()
    {
        if (stateFlags == null) stateFlags = GetComponentInChildren<StateFlags>(true);
        if (stateFlags == null) stateFlags = GetComponentInParent<StateFlags>();
    }

    protected override SaveData Capture()
    {
        if (stateFlags == null) return null;

        var data = new SaveData();
        foreach (Entry e in flagsToSave)
        {
            if (e == null || e.flag == null || string.IsNullOrEmpty(e.key)) continue;
            if (!stateFlags.Contains(e.flag)) continue;

            data.flags.Add(new SavedFlag { key = e.key, value = stateFlags.Get(e.flag) });
        }
        return data;
    }

    protected override void Restore(SaveData state, int version)
    {
        if (stateFlags == null) return;

        foreach (SavedFlag saved in state.flags)
        {
            if (saved == null) continue;

            FlagSO flag = Find(saved.key);
            if (flag == null)
            {
                Debug.LogWarning($"[Save] Saved flag '{saved.key}' is not in the FlagSaveable list. Skipped.", this);
                continue;
            }

            if (!stateFlags.Contains(flag))
            {
                Debug.LogWarning($"[Save] Flag '{flag.name}' is not registered on {stateFlags.name}. Skipped.", this);
                continue;
            }

            stateFlags.Set(flag, saved.value);
        }
    }

    private FlagSO Find(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (Entry e in flagsToSave)
        {
            if (e != null && e.key == key) return e.flag;
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var seen = new HashSet<string>();
        foreach (Entry e in flagsToSave)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.key) && e.flag != null) e.key = e.flag.name;
            if (!string.IsNullOrEmpty(e.key) && !seen.Add(e.key))
                Debug.LogWarning($"[Save] FlagSaveable on {name}: the key '{e.key}' is used twice.", this);
        }
    }
#endif
}
