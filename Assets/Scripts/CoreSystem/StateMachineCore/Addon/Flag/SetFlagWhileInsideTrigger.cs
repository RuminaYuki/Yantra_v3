using System.Collections.Generic;
using UnityEngine;

public class SetFlagWhileInsideTrigger : MonoBehaviour
{
    public enum FlagTarget
    {
        Other,  // set the flag on whatever enters the trigger
        Owner,  // set the flag on this trigger's own StateFlagsAccess
    }

    public enum SetMode
    {
        Continuous,    // set true every physics step while inside (re-sets it if something else clears it); false on exit
        OncePerEnter,  // set true once per entry (stays false if something clears it); false on exit; fires again on next entry
        OnceEver,      // set true the first time only; never set back to false, never fires again
    }

    [SerializeField] private FlagSO flag;
    [SerializeField] private FlagTarget setOn = FlagTarget.Other;
    [SerializeField] private SetMode mode = SetMode.Continuous;

    [Tooltip("Owner only. Empty = find StateFlagsAccess on this object or its parents.")]
    [SerializeField] private StateFlagsAccess owner;

    [Tooltip("Only colliders on these layers count.")]
    [SerializeField] private LayerMask detectLayers = ~0;

    // Colliders currently inside. Used to tell "just entered" from "still inside",
    // and (Owner) to keep the flag on until every collider has left.
    private readonly HashSet<Collider> _inside = new HashSet<Collider>();

    // OnceEver: true after the flag has been set the first time.
    private bool _hasFired;

    private void Awake()
    {
        if (owner == null) owner = GetComponentInParent<StateFlagsAccess>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (flag == null || !IsDetected(other) || !enabled) return;
        if (mode == SetMode.OnceEver && _hasFired) return;

        StateFlagsAccess target = TargetFor(other);
        if (target == null) return;

        // HashSet.Add returns true only the first time this collider is added = it just entered.
        bool justEntered = _inside.Add(other);
        if (mode != SetMode.Continuous && !justEntered) return;

        target.Set(flag, true);
        _hasFired = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (flag == null || !IsDetected(other) || !enabled) return;

        _inside.Remove(other);
        _inside.RemoveWhere(c => c == null || !c.enabled);   // destroyed/disabled colliders never send Exit

        if (mode == SetMode.OnceEver) return;   // OnceEver keeps the flag true

        if (setOn == FlagTarget.Other)
        {
            StateFlagsAccess stateFlags = other.GetComponentInParent<StateFlagsAccess>();
            if (stateFlags != null) stateFlags.Set(flag, false);
            return;
        }

        if (owner != null && _inside.Count == 0) owner.Set(flag, false);
    }

    private void OnDisable()
    {
        _inside.Clear();   // re-enabling counts everyone inside as a fresh entry

        if (setOn != FlagTarget.Owner || mode == SetMode.OnceEver) return;
        if (owner != null && flag != null) owner.Set(flag, false);
    }

    private StateFlagsAccess TargetFor(Collider other)
    {
        return setOn == FlagTarget.Owner ? owner : other.GetComponentInParent<StateFlagsAccess>();
    }

    private bool IsDetected(Collider other)
    {
        return (detectLayers.value & (1 << other.gameObject.layer)) != 0;
    }
}
