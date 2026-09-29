using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detection only: fires events when something enters/exits the zone.
/// It doesn't know what should happen next (open a door, cutscene, sound...);
/// put that in a separate component that subscribes to these events.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Only these layers are counted (layers are safer than tags).")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Header("Behaviour")]
    [Tooltip("Fire only once: after the first Enter, no more events are raised.")]
    [SerializeField] private bool triggerOnce;

    /// <summary>Raised when the first collider of an object enters the zone.</summary>
    public event Action<GameObject> OnZoneEnter;
    /// <summary>Raised when the last collider of an object leaves the zone.</summary>
    public event Action<GameObject> OnZoneExit;

    // key = root object, value = how many of its colliders are inside right now.
    // A player often has several colliders; without counting, Enter would fire repeatedly.
    private readonly Dictionary<GameObject, int> _occupants = new();
    private bool _consumed;

    public bool IsOccupied => _occupants.Count > 0;
    public bool Contains(GameObject root) => _occupants.ContainsKey(root);

    private void Reset()
    {
        // Auto-set isTrigger when the component is added.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed || !PassesFilter(other)) return;

        GameObject root = GetRoot(other);
        if (_occupants.TryGetValue(root, out int count))
        {
            _occupants[root] = count + 1;
            return;
        }

        _occupants[root] = 1;
        OnZoneEnter?.Invoke(root);

        if (triggerOnce) _consumed = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!PassesFilter(other)) return;

        GameObject root = GetRoot(other);
        if (!_occupants.TryGetValue(root, out int count)) return;

        if (count > 1)
        {
            _occupants[root] = count - 1;
            return;
        }

        _occupants.Remove(root);
        OnZoneExit?.Invoke(root);
    }

    private void OnDisable()
    {
        // OnTriggerExit isn't guaranteed when the zone is disabled, so raise Exit manually
        // to let listeners restore anything they locked on Enter.
        if (_occupants.Count == 0) return;

        var snapshot = new List<GameObject>(_occupants.Keys);
        _occupants.Clear();
        foreach (var root in snapshot)
            OnZoneExit?.Invoke(root);
    }

    private bool PassesFilter(Collider other)
    {
        return (targetLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    // Use the Rigidbody's object as the root if present, otherwise the top of the hierarchy.
    // (CharacterController has no Rigidbody, so it falls back to transform.root.)
    private static GameObject GetRoot(Collider other)
    {
        return other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.transform.root.gameObject;
    }
}
