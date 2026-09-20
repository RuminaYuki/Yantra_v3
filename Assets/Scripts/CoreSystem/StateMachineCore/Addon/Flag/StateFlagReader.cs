using System.Collections.Generic;
using UnityEngine;

public class StateFlagReader : StateFlagsAccess
{
    [SerializeField] private StateFlagsAccess target;
    [SerializeField] private List<FlagSO> flags = new();

    public override bool Get(FlagSO flag)
    {
        if (target == null || target == this || flag == null) return false;
        return target.Get(flag);
    }

    public override void Set(FlagSO flag, bool value)
    {
        if (target == null || target == this)
        {
            Debug.LogError($"StateFlagReader on {name} needs a valid target.", this);
            return;
        }

        target.Set(flag, value);
    }

    public bool Get(int index)
    {
        if (index < 0 || index >= flags.Count) return false;
        return Get(flags[index]);
    }

    public bool AllTrue
    {
        get
        {
            if (flags.Count == 0) return false;

            foreach (FlagSO flag in flags)
            {
                if (!Get(flag)) return false;
            }

            return true;
        }
    }

    public bool AnyTrue
    {
        get
        {
            foreach (FlagSO flag in flags)
            {
                if (Get(flag)) return true;
            }

            return false;
        }
    }

    public bool AllFalse => !AnyTrue;

    private void OnValidate()
    {
        if (target == this)
        {
            Debug.LogWarning($"StateFlagReader on {name} cannot target itself.", this);
            target = null;
        }
    }
}
