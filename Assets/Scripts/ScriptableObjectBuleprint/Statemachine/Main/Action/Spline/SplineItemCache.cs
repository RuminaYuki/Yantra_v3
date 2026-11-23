using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

// Keeps the items SplineInstantiate made along the spline (e.g. Vengeful Roots' thorns), so they're
// made once and then only turned on / off, instead of being made and destroyed on every spawn.
//
// Why: SplineInstantiate destroys its items whenever it's turned off (OnDisable → Clear) or its
// prefab is swapped, and makes them all again in one frame. A thorn has an Animator and ~40 objects,
// so a whole pattern of them in one frame is a visible hitch.
//
// How: the first time a prefab is shown, SplineInstantiate places it once (same spacing / seed, so the
// same layout as before). The places are copied onto our own items under "<prefab> (Cached)", and
// SplineInstantiate's own items are removed. Later spawns just turn that group on again.
//
// SwapSplineItemAction adds this component by itself, nothing to set up.
// The groups are children of this object, so the Spline Items actions / conditions still find them.
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineInstantiate))]
public class SplineItemCache : MonoBehaviour
{
    // Name SplineInstantiate gives its hidden parent of the items ("root-" + id).
    private const string InstancesRootPrefix = "root-";

    private readonly Dictionary<GameObject, GameObject> _groups = new();
    private SplineInstantiate _spline;

    private void Awake()
    {
        _spline = GetComponent<SplineInstantiate>();
    }

    // Turns on the items of this prefab and turns off every other group.
    public void Show(GameObject prefab)
    {
        if (prefab == null)
            return;

        if (!_groups.TryGetValue(prefab, out GameObject group) || group == null)
        {
            group = Build(prefab);
            if (group == null)
                return;
            _groups[prefab] = group;
        }

        foreach (GameObject other in _groups.Values)
        {
            if (other != null && other != group)
                other.SetActive(false);
        }

        // Off then on: the items start fresh (Animator back to its default state, particles replay,
        // DamageHitbox forgets who it hit) even when the same prefab is shown twice in a row.
        group.SetActive(false);
        group.SetActive(true);
    }

    // Builds this prefab's group ahead of time and leaves it off (e.g. at scene load), so Show is
    // only a switch later.
    public void Prewarm(GameObject prefab)
    {
        if (prefab == null || (_groups.TryGetValue(prefab, out GameObject existing) && existing != null))
            return;

        GameObject group = Build(prefab);   // made off already
        if (group != null)
            _groups[prefab] = group;
    }

    // Turns every group off, e.g. before going back to the pool.
    public void HideAll()
    {
        foreach (GameObject group in _groups.Values)
        {
            if (group != null)
                group.SetActive(false);
        }
    }

    // Lets SplineInstantiate place the prefab once, copies the places onto our own items, then removes its items.
    private GameObject Build(GameObject prefab)
    {
        if (_spline == null)
            _spline = GetComponent<SplineInstantiate>();

        _spline.itemsToInstantiate = new[]
        {
            new SplineInstantiate.InstantiableItem { Prefab = prefab, Probability = 100f }
        };
        _spline.UpdateInstances();

        Transform instancesRoot = FindInstancesRoot();
        if (instancesRoot == null)
        {
            Debug.LogWarning($"[SplineItemCache] SplineInstantiate made no items for {prefab.name} (empty spline?).", this);
            ClearSpline();
            return null;
        }

        // Made off, so the items don't wake up and play before Show turns them on.
        var group = new GameObject($"{prefab.name} (Cached)");
        group.SetActive(false);
        group.transform.SetParent(transform, false);

        for (int i = 0; i < instancesRoot.childCount; i++)
        {
            Transform placed = instancesRoot.GetChild(i);
            GameObject item = Instantiate(prefab, group.transform);
            item.transform.SetPositionAndRotation(placed.position, placed.rotation);
            item.transform.localScale = placed.localScale;
        }

        ClearSpline();
        return group;
    }

    // No items left in SplineInstantiate, so it has nothing to remake when it's turned on again.
    private void ClearSpline()
    {
        _spline.itemsToInstantiate = System.Array.Empty<SplineInstantiate.InstantiableItem>();
        _spline.Clear();
    }

    private Transform FindInstancesRoot()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith(InstancesRootPrefix) && child.childCount > 0)
                return child;
        }
        return null;
    }
}
