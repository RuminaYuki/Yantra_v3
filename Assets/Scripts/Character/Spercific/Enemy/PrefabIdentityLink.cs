using System;
using System.Collections.Generic;
using UnityEngine;
using Yuki.Learning.StateMachine;

/// <summary>
/// Like TargetLink, but finds each target through a PrefabIdentity: first the object whose Prefab Id
/// matches (e.g. "Tanee"), then, by name, an object inside it (e.g. "EnemyController"). Empty name =
/// the object holding the PrefabIdentity. Put it on the same GameObject as the StateMachineController
/// and Provider.
/// Tries in Awake, so targets already in the scene are linked before the state machine starts. Targets
/// that aren't there yet (spawned later) are looked for again every RetryInterval until found.
/// </summary>
[RequireComponent(typeof(Provider))]
public class PrefabIdentityLink : MonoBehaviour
{
    [Serializable]
    private struct Entry
    {
        [Tooltip("The anchor the Actions/Conditions use for this object")]
        public AnchorIDSO Anchor;
        [Tooltip("The Prefab Id on the target's PrefabIdentity, e.g. Tanee")]
        public string PrefabId;
        [Tooltip("An object by name: looked for in its children first, then in its siblings and theirs. " +
                 "Empty = the PrefabIdentity's object")]
        public string Name;
    }

    private const float RetryInterval = 0.5f;

    [SerializeField] private List<Entry> _targets = new List<Entry>
    {
        new Entry { PrefabId = "Tanee", Name = "EnemyController" }
    };

    private Provider _provider;
    private readonly List<Entry> _waiting = new List<Entry>();
    private float _nextRetry;

    private void Awake()
    {
        _provider = GetComponent<Provider>();

        foreach (Entry entry in _targets)
        {
            if (entry.Anchor == null || string.IsNullOrEmpty(entry.PrefabId))
            {
                Debug.LogError("[PrefabIdentityLink] A row has no Anchor or no Prefab Id.", this);
                continue;
            }

            if (!TryLink(entry))
            {
                Debug.LogWarning(
                    $"[PrefabIdentityLink] '{entry.Anchor.name}': nothing with Prefab Id '{entry.PrefabId}' " +
                    $"and name '{Describe(entry.Name)}' in the scene yet. Still looking (it may be spawned later).",
                    this);
                _waiting.Add(entry);
            }
        }

        enabled = _waiting.Count > 0;
    }

    // Only runs while something is still missing.
    private void Update()
    {
        if (Time.time < _nextRetry)
        {
            return;
        }

        _nextRetry = Time.time + RetryInterval;
        _waiting.RemoveAll(TryLink);
        enabled = _waiting.Count > 0;
    }

    private bool TryLink(Entry entry)
    {
        GameObject target = Find(entry.PrefabId, entry.Name);
        if (target == null)
        {
            return false;
        }

        _provider.Set(entry.Anchor, target);
        return true;
    }

    private static GameObject Find(string prefabId, string name)
    {
        foreach (PrefabIdentity identity in FindObjectsByType<PrefabIdentity>(FindObjectsInactive.Exclude))
        {
            if (identity.PrefabId != prefabId)
            {
                continue;
            }

            if (string.IsNullOrEmpty(name))
            {
                return identity.gameObject;
            }

            // First the object itself and its children; then its siblings and their children
            // (e.g. the model next to EnemyController under the same prefab root).
            GameObject found = FindByName(identity.transform, name);
            if (found == null && identity.transform.parent != null)
            {
                found = FindByName(identity.transform.parent, name);
            }

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // `root` and everything under it, inactive objects too, at any depth.
    private static GameObject FindByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static string Describe(string value) => string.IsNullOrEmpty(value) ? "(itself)" : value;
}
