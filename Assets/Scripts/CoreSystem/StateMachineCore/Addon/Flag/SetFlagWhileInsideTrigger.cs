using System.Collections.Generic;
using UnityEngine;

public class SetFlagWhileInsideTrigger : MonoBehaviour
{
    public enum FlagTarget
    {
        Other,  // set the flag on whatever enters the trigger
        Owner,  // set the flag on this trigger's own StateFlagsAccess
    }

    [SerializeField] private FlagSO flag;
    [SerializeField] private FlagTarget setOn = FlagTarget.Other;

    [Tooltip("Owner only. Empty = find StateFlagsAccess on this object or its parents.")]
    [SerializeField] private StateFlagsAccess owner;

    [Tooltip("Only colliders on these layers count.")]
    [SerializeField] private LayerMask detectLayers = ~0;

    // Owner mode: the flag stays on until every collider inside has left.
    private readonly HashSet<Collider> _inside = new HashSet<Collider>();

    private void Awake()
    {
        if (owner == null) owner = GetComponentInParent<StateFlagsAccess>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (flag == null || !IsDetected(other) || !enabled) return;

        if (setOn == FlagTarget.Other)
        {
            StateFlagsAccess stateFlags = other.GetComponentInParent<StateFlagsAccess>();
            if (stateFlags != null) stateFlags.Set(flag, true);
            return;
        }

        if (owner == null) return;
        _inside.Add(other);
        owner.Set(flag, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (flag == null || !IsDetected(other) || !enabled) return;

        if (setOn == FlagTarget.Other)
        {
            StateFlagsAccess stateFlags = other.GetComponentInParent<StateFlagsAccess>();
            if (stateFlags != null) stateFlags.Set(flag, false);
            return;
        }

        _inside.Remove(other);
        _inside.RemoveWhere(c => c == null || !c.enabled);   // destroyed/disabled colliders never send Exit
        if (owner != null && _inside.Count == 0) owner.Set(flag, false);
    }

    private void OnDisable()
    {
        if (setOn != FlagTarget.Owner) return;

        _inside.Clear();
        if (owner != null && flag != null) owner.Set(flag, false);
    }

    private bool IsDetected(Collider other)
    {
        return (detectLayers.value & (1 << other.gameObject.layer)) != 0;
    }
}
