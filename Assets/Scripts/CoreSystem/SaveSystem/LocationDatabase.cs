using System.Collections.Generic;
using UnityEngine;

// Turns a locationId from a save file back into its LocationData (name + image).
// A save file can only hold text, so the Load Save screen looks the image up here.
//
// After adding new locations: right-click this asset's header > "Collect All Locations".
// When an id is not found, use FallbackName / FallbackImage instead.
[CreateAssetMenu(fileName = "LocationDatabase", menuName = "Save/Location Database")]
public class LocationDatabase : ScriptableObject
{
    [SerializeField] private List<LocationData> locations = new List<LocationData>();

    [Header("Fallback (id missing or not found)")]
    [SerializeField] private string fallbackName = "Unknown";
    [SerializeField] private Sprite fallbackImage;

    private Dictionary<string, LocationData> map;

    public IReadOnlyList<LocationData> Locations => locations;
    public string FallbackName => fallbackName;
    public Sprite FallbackImage => fallbackImage;

    public bool TryGet(string id, out LocationData location)
    {
        location = null;
        if (string.IsNullOrEmpty(id)) return false;
        if (map == null) BuildMap();
        return map.TryGetValue(id, out location) && location != null;
    }

    // Name to show for this id, or the fallback name.
    public string GetName(string id)
    {
        return TryGet(id, out LocationData location) && !string.IsNullOrEmpty(location.locationName)
            ? location.locationName
            : fallbackName;
    }

    // Image to show for this id, or the fallback image.
    public Sprite GetImage(string id)
    {
        return TryGet(id, out LocationData location) && location.locationImage != null
            ? location.locationImage
            : fallbackImage;
    }

    private void OnEnable() => map = null;

    private void BuildMap()
    {
        map = new Dictionary<string, LocationData>();
        foreach (LocationData location in locations)
        {
            if (location == null || string.IsNullOrEmpty(location.locationId)) continue;

            if (map.TryGetValue(location.locationId, out LocationData other) && other != location)
            {
                Debug.LogWarning($"[Save] LocationDatabase: '{location.name}' and '{other.name}' share the id " +
                                 $"'{location.locationId}'. Only the first one is used.", this);
                continue;
            }
            map[location.locationId] = location;
        }
    }

#if UNITY_EDITOR
    private void OnValidate() => map = null;

    [ContextMenu("Collect All Locations")]
    private void CollectAllLocations()
    {
        locations.Clear();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:LocationData"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            LocationData location = UnityEditor.AssetDatabase.LoadAssetAtPath<LocationData>(path);
            if (location != null) locations.Add(location);
        }
        locations.Sort((a, b) => string.CompareOrdinal(a.locationId, b.locationId));

        map = null;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);

        Debug.Log($"[Save] LocationDatabase: collected {locations.Count} locations.", this);
        CheckIds();
    }

    [ContextMenu("Check Ids")]
    private void CheckIds()
    {
        var seen = new Dictionary<string, LocationData>();
        int problems = 0;

        foreach (LocationData location in locations)
        {
            if (location == null) continue;

            if (string.IsNullOrEmpty(location.locationId))
            {
                Debug.LogWarning($"[Save] LocationDatabase: '{location.name}' has no id.", location);
                problems++;
            }
            else if (seen.TryGetValue(location.locationId, out LocationData other))
            {
                Debug.LogWarning($"[Save] LocationDatabase: '{location.name}' and '{other.name}' share the id " +
                                 $"'{location.locationId}'. Duplicated assets keep the old id: give one a new id.", location);
                problems++;
            }
            else
            {
                seen.Add(location.locationId, location);
            }
        }

        if (fallbackImage == null)
            Debug.LogWarning("[Save] LocationDatabase: no fallback image set.", this);

        if (problems == 0) Debug.Log("[Save] LocationDatabase: all ids are fine.", this);
    }
#endif
}
