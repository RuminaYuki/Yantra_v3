using UnityEngine;
using Yuki.Learning.StateMachine;

/// <summary>
/// Shared lookup for the Component modifiers: anchor empty = the owner itself,
/// otherwise the object linked to that anchor in the owner's Provider. Logs when it can't find it.
/// </summary>
public static class ComponentModifierTarget
{
    public static bool TryGet<T>(GameObject owner, AnchorIDSO anchor, Object modifier, out T component)
    {
        component = default;
        GameObject target = owner;

        if (anchor != null &&
            !(owner.TryGetComponent(out Provider provider) && provider.TryGet(anchor, out target)))
        {
            Debug.LogError($"[{modifier.name}] '{anchor.name}' is not linked in the Provider on '{owner.name}'.", owner);
            return false;
        }

        if (target.TryGetComponent(out component))
            return true;

        Debug.LogError($"[{modifier.name}] needs {typeof(T).Name} on '{target.name}'.", target);
        return false;
    }
}
