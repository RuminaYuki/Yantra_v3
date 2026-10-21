using System.Collections.Generic;
using UnityEngine;

// A place the player can appear at after a scene loads: a SavePoint,
// or the spot in front of a mini-game entrance (SaveManager.GoToScene).
// Found by the id of its SaveId component.
[RequireComponent(typeof(SaveId))]
public class SpawnPoint : MonoBehaviour
{
    private static readonly Dictionary<string, SpawnPoint> all = new Dictionary<string, SpawnPoint>();

    [Tooltip("Where the player stands and which way they face. Empty = this object.\n" +
             "Use a child object so the player does not appear inside the shrine/statue.")]
    [SerializeField] private Transform spawnAt;

    private SaveId saveId;

    public string Id => saveId != null ? saveId.Id : null;
    public Transform SpawnTransform => spawnAt != null ? spawnAt : transform;

    public static bool TryGet(string id, out SpawnPoint point)
    {
        point = null;
        if (string.IsNullOrEmpty(id) || !all.TryGetValue(id, out point)) return false;
        if (point == null) // destroyed without OnDestroy (should not happen, but Unity null is cheap to check)
        {
            all.Remove(id);
            return false;
        }
        return true;
    }

    protected virtual void Awake()
    {
        saveId = GetComponent<SaveId>();
        string id = Id;
        if (string.IsNullOrEmpty(id)) return;

        if (all.TryGetValue(id, out SpawnPoint other) && other != null && other != this)
            Debug.LogWarning($"[Save] {name} and {other.name} have the same SaveId.", this);

        all[id] = this;
    }

    protected virtual void OnDestroy()
    {
        string id = Id;
        if (id != null && all.TryGetValue(id, out SpawnPoint current) && current == this) all.Remove(id);
    }

    private void OnDrawGizmos()
    {
        Transform t = SpawnTransform;
        Gizmos.color = new Color(0.2f, 0.8f, 1f);
        Gizmos.DrawWireSphere(t.position + Vector3.up * 0.1f, 0.3f);
        Gizmos.DrawLine(t.position + Vector3.up * 0.1f, t.position + Vector3.up * 0.1f + t.forward * 0.8f);
    }
}
