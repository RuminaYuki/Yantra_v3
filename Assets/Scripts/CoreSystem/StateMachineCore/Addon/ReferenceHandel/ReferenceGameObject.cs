using System;
using UnityEngine;

/// <summary>
/// Lets a state machine reach components that are not on its owner (children, other objects).
/// Put it on the same GameObject as the StateMachineController and drag the targets in.
/// Lookup is by type only: if two targets have the same component, the one higher in the list wins.
/// </summary>
public class ReferenceGameObject : MonoBehaviour
{
    [SerializeField] private GameObject[] _targets = Array.Empty<GameObject>();

    // No `where T : Component`, so interfaces (e.g. Iinteractor) work too.
    public bool TryGet<T>(out T component)
    {
        foreach (GameObject target in _targets)
        {
            if (target != null && target.TryGetComponent(out component))
            {
                return true;
            }
        }

        component = default;
        return false;
    }

    // For the editor check, which only has a System.Type.
    public bool Has(Type componentType)
    {
        foreach (GameObject target in _targets)
        {
            if (target != null && target.GetComponent(componentType) != null)
            {
                return true;
            }
        }

        return false;
    }
}
