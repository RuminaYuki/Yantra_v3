using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detection only: fires events when something enters/exits the zone.
/// It doesn't know what should happen next (open a door, cutscene, sound...);
/// put that in a separate component that subscribes to these events.
///
/// Teleport-safe: Unity sends no OnTriggerExit when a collider is disabled while inside
/// (e.g. the CharacterController is turned off to teleport the player), but sends OnTriggerEnter
/// again when it's turned back on. So this zone remembers each collider that is inside (re-entering
/// with the same one is ignored), and while occupied it checks every physics step that those
/// colliders are still here, raising Exit itself for any that were moved away or destroyed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Only these layers are counted (layers are safer than tags).")]
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private string tagFilter = "";

    [Header("Behaviour")]
    [Tooltip("Fire only once: after the first Enter, no more events are raised.")]
    [SerializeField] private bool triggerOnce;

    /// <summary>Raised when the first collider of an object enters the zone.</summary>
    public event Action<GameObject> OnZoneEnter;
    /// <summary>Raised when the last collider of an object leaves the zone.</summary>
    public event Action<GameObject> OnZoneExit;

    // How far (metres, each side) a collider's bounds may be from the zone before it counts as gone.
    // Only used for the teleport check, so it can be generous: a wrong "gone" while the player is still
    // inside would be worse (Unity won't send Enter again for a pair it thinks never ended).
    private const float StaleMargin = 0.5f;

    // key = root object, value = its colliders that are inside right now.
    // A player often has several colliders; Enter fires on the first, Exit on the last.
    private readonly Dictionary<GameObject, HashSet<Collider>> _occupants = new();
    private bool _consumed;

    private Collider[] _ownColliders;

    // Reused every physics step so the check doesn't allocate.
    private readonly List<GameObject> _staleRoots = new();
    private readonly List<Collider> _staleColliders = new();

    public bool IsOccupied => _occupants.Count > 0;
    public bool Contains(GameObject root) => _occupants.ContainsKey(root);

    private void Reset()
    {
        // Auto-set isTrigger when the component is added.
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake() => CacheOwnColliders();

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed || !PassesFilter(other)) return;

        GameObject root = GetRoot(other);
        if (_occupants.TryGetValue(root, out HashSet<Collider> inside))
        {
            // Already inside: remember this collider too. The same collider entering again
            // (switched off and on to teleport) is ignored by the set, so nothing gets counted twice.
            inside.Add(other);
            return;
        }

        _occupants[root] = new HashSet<Collider> { other };
        OnZoneEnter?.Invoke(root);

        if (triggerOnce) _consumed = true;
    }

    private void OnTriggerExit(Collider other)
    {
        // No filter check: only filtered colliders are ever remembered, and the object's layer/tag
        // may have changed while it was inside. Forgetting it is always right.
        GameObject root = FindRootOf(other);
        if (root != null) RemoveCollider(root, other);
    }

    private void FixedUpdate()
    {
        if (_occupants.Count == 0) return;

        // Zone switched off for now (colliders disabled, or turned solid) — keep what we know,
        // same as Unity does. When it's switched back on, Unity sends Enter for whoever is inside.
        if (!HasActiveTrigger()) return;

        _staleRoots.Clear();
        _staleColliders.Clear();

        foreach (KeyValuePair<GameObject, HashSet<Collider>> pair in _occupants)
        {
            foreach (Collider col in pair.Value)
            {
                if (!IsGone(col)) continue;
                _staleRoots.Add(pair.Key);
                _staleColliders.Add(col);
            }
        }

        // Removed after the loop: RemoveCollider can raise Exit, and listeners must not see half-edited sets.
        for (int i = 0; i < _staleColliders.Count; i++)
            RemoveCollider(_staleRoots[i], _staleColliders[i]);
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

    // ---------- Occupants ----------

    private void RemoveCollider(GameObject root, Collider col)
    {
        if (!_occupants.TryGetValue(root, out HashSet<Collider> inside)) return;
        if (!inside.Remove(col)) return;
        if (inside.Count > 0) return;   // the object still has other colliders inside

        _occupants.Remove(root);
        OnZoneExit?.Invoke(root);
    }

    private GameObject FindRootOf(Collider col)
    {
        // Usually the same root as on Enter...
        GameObject root = GetRoot(col);
        if (_occupants.TryGetValue(root, out HashSet<Collider> inside) && inside.Contains(col))
            return root;

        // ...but the object may have been re-parented while inside (e.g. snapped to another object).
        foreach (KeyValuePair<GameObject, HashSet<Collider>> pair in _occupants)
            if (pair.Value.Contains(col)) return pair.Key;

        return null;
    }

    // Collider no longer in the zone, and Unity won't tell us (no OnTriggerExit for these cases).
    private bool IsGone(Collider col)
    {
        if (col == null) return true;   // destroyed

        // Switched off for now (Glory move, cutscene) — still counts as inside, like before.
        // If it's switched back on somewhere else, the bounds check below catches it then.
        if (!col.enabled || !col.gameObject.activeInHierarchy) return false;

        Bounds bounds = col.bounds;
        bounds.Expand(StaleMargin * 2f);   // Expand grows the total size, so ×2 = margin on each side
        return !OverlapsZone(bounds);
    }

    // ---------- Zone colliders ----------

    private void CacheOwnColliders()
    {
        if (!TryGetComponent(out Rigidbody body))
        {
            _ownColliders = GetComponents<Collider>();
            return;
        }

        // Kinematic Rigidbody on the zone = child colliders belong to it too (one compound trigger).
        var list = new List<Collider>();
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col.GetComponentInParent<Rigidbody>(true) == body)
                list.Add(col);
        }
        _ownColliders = list.ToArray();
    }

    private bool HasActiveTrigger()
    {
        foreach (Collider own in _ownColliders)
        {
            if (own != null && own.enabled && own.isTrigger) return true;
        }
        return false;
    }

    // AABB test: may say "overlaps" a little outside a rotated box, never says "doesn't" when it does.
    private bool OverlapsZone(Bounds bounds)
    {
        foreach (Collider own in _ownColliders)
        {
            if (own != null && own.enabled && own.isTrigger && own.bounds.Intersects(bounds))
                return true;
        }
        return false;
    }

    // ---------- Filter ----------

    private bool PassesFilter(Collider other)
    {
        bool layerOk = (targetLayers.value & (1 << other.gameObject.layer)) != 0;
        bool tagOk = string.IsNullOrEmpty(tagFilter) || other.CompareTag(tagFilter);
        return layerOk && tagOk;
    }

    // Use the Rigidbody's object as the root if present, otherwise the top of the hierarchy.
    // (CharacterController has no Rigidbody, so it falls back to transform.root.)
    private static GameObject GetRoot(Collider other)
    {
        return other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.transform.root.gameObject;
    }

    public void Clear()
    {
        _occupants.Clear();
        _consumed = false;
    }
}
