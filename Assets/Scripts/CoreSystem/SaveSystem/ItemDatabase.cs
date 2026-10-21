using System.Collections.Generic;
using UnityEngine;

// Turns an ItemData.id from a save file back into the ItemData asset.
// A save file can only hold text, not asset references, so items are saved by id.
//
// After adding new items: right-click this asset's header > "Collect All Items".
// An item missing from this list is lost when a save is loaded (a warning is logged when saving).
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Save/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemData> items = new List<ItemData>();

    private Dictionary<string, ItemData> map;

    public IReadOnlyList<ItemData> Items => items;

    public bool TryGet(string id, out ItemData item)
    {
        item = null;
        if (string.IsNullOrEmpty(id)) return false;
        if (map == null) BuildMap();
        return map.TryGetValue(id, out item) && item != null;
    }

    public bool Contains(ItemData item) => item != null && TryGet(item.id, out ItemData found) && found == item;

    private void OnEnable() => map = null;

    private void BuildMap()
    {
        map = new Dictionary<string, ItemData>();
        foreach (ItemData item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;

            if (map.TryGetValue(item.id, out ItemData other) && other != item)
            {
                Debug.LogWarning($"[Save] ItemDatabase: '{item.name}' and '{other.name}' share the id '{item.id}'. " +
                                 "Only the first one can be loaded.", this);
                continue;
            }
            map[item.id] = item;
        }
    }

#if UNITY_EDITOR
    private void OnValidate() => map = null;

    [ContextMenu("Collect All Items")]
    private void CollectAllItems()
    {
        items.Clear();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ItemData"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null) items.Add(item);
        }
        items.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        map = null;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);

        Debug.Log($"[Save] ItemDatabase: collected {items.Count} items.", this);
        CheckIds();
    }

    [ContextMenu("Check Ids")]
    private void CheckIds()
    {
        var seen = new Dictionary<string, ItemData>();
        int problems = 0;

        foreach (ItemData item in items)
        {
            if (item == null) continue;

            if (string.IsNullOrEmpty(item.id))
            {
                Debug.LogWarning($"[Save] ItemDatabase: '{item.name}' has no id.", item);
                problems++;
            }
            else if (seen.TryGetValue(item.id, out ItemData other))
            {
                Debug.LogWarning($"[Save] ItemDatabase: '{item.name}' and '{other.name}' share the id '{item.id}'. " +
                                 "Duplicated assets keep the old id: give one of them a new id.", item);
                problems++;
            }
            else
            {
                seen.Add(item.id, item);
            }
        }

        if (problems == 0) Debug.Log("[Save] ItemDatabase: all ids are fine.", this);
    }
#endif
}
