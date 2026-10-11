using System;
using System.Collections.Generic;
using UnityEngine;
using Yuki.Learning.StateMachine;

/// <summary>
/// Put it on the same GameObject as the StateMachineController and Provider.
/// A prefab can't drag scene objects (the player, the player's IK target...) into its Provider,
/// so this finds each one in Awake by tag and/or name and links it under its anchor.
/// Runs in Awake, so the links exist before the state machine starts.
/// </summary>
[RequireComponent(typeof(Provider))]
public class TargetLink : MonoBehaviour
{
    [Serializable]
    private struct Entry
    {
        [Tooltip("The anchor the Actions/Conditions use for this object")]
        public AnchorIDSO Anchor;
        [Tooltip("Empty = any tag")]
        public string Tag;
        [Tooltip("Empty = any name")]
        public string Name;
    }

    [SerializeField] private List<Entry> _targets = new List<Entry>
    {
        new Entry { Tag = "Player", Name = "PlayerController" }
    };

    private void Awake()
    {
        Provider provider = GetComponent<Provider>();

        foreach (Entry entry in _targets)
        {
            if (entry.Anchor == null)
            {
                Debug.LogError("[TargetLink] A row has no Anchor.", this);
                continue;
            }

            GameObject target = Find(entry.Tag, entry.Name);
            if (target == null)
            {
                Debug.LogError(
                    $"[TargetLink] '{entry.Anchor.name}': nothing in the scene with " +
                    $"tag '{Describe(entry.Tag)}' and name '{Describe(entry.Name)}'.", this);
                continue;
            }

            provider.Set(entry.Anchor, target);
        }
    }

    private static GameObject Find(string tag, string name)
    {
        bool hasTag = !string.IsNullOrEmpty(tag);
        bool hasName = !string.IsNullOrEmpty(name);

        if (!hasTag)
        {
            // Name only. GameObject.Find only sees active objects.
            return hasName ? GameObject.Find(name) : null;
        }

        foreach (GameObject candidate in GameObject.FindGameObjectsWithTag(tag))
        {
            if (!hasName || candidate.name == name)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string Describe(string value) => string.IsNullOrEmpty(value) ? "(any)" : value;
}
