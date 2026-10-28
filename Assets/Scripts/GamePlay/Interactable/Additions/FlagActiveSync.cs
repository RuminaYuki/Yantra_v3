using System;
using System.Collections.Generic;
using UnityEngine;

// Re-applies SetActive from a StateFlag when the scene starts,
// so objects toggled by FlagAddition stay correct after save/load.
// Put it on an object that is always active (e.g. the button itself).
// Runs after FlagSaveable (-50), so it reads the loaded flag value.
public class FlagActiveSync : MonoBehaviour
{
    [Serializable]
    public struct Target
    {
        public GameObject obj;
        [Tooltip("Active state to apply when the flag equals Flag Value.")]
        public bool activeWhenReached;
    }

    [SerializeField] private FlagSO flag;
    [SerializeField] private bool flagValue = true;

    [Tooltip("Empty = player StateFlags found by the 'Player' tag.")]
    [SerializeField] private StateFlagsAccess stateFlags;

    [SerializeField] private List<Target> targets = new();

    private void Start()
    {
        Apply();
    }

    // Can also be called from InteractorEvent instead of SetActive.
    public void Apply()
    {
        if (flag == null) return;

        if (stateFlags == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) stateFlags = player.GetComponentInChildren<StateFlags>();
        }
        if (stateFlags == null)
        {
            Debug.LogWarning($"[FlagActiveSync] No StateFlags found for {name}.", this);
            return;
        }

        bool reached = stateFlags.Get(flag) == flagValue;
        foreach (Target t in targets)
        {
            if (t.obj == null) continue;
            // Before the flag is reached, leave objects as placed in the scene.
            if (reached) t.obj.SetActive(t.activeWhenReached);
        }
    }
}
