using System;
using System.Collections.Generic;
using UnityEngine;

// Saves every item on the inventory board: which item, how many, where and how it is rotated.
// Put it on the player root (next to PlayerSaveable).
//
// Items are saved by ItemData.id and turned back into assets with the ItemDatabase.
// Bags are placed before other items on load, because their cells must exist first.
[DisallowMultipleComponent]
public class InventorySaveable : SaveableBehaviour<InventorySaveable.SaveData>
{
    [Serializable]
    public class Entry
    {
        public string itemId;
        public int qty;
        public int x;
        public int y;
        public int rot;
    }

    [Serializable]
    public class SaveData
    {
        public List<Entry> items = new List<Entry>();
    }

    [Tooltip("Empty = found on this object or its children.")]
    [SerializeField] private PlayerInventory playerInventory;

    [Tooltip("Turns saved item ids back into ItemData assets.")]
    [SerializeField] private ItemDatabase database;

    protected override string DefaultKey => "inventory";

    private Inventory Inv => playerInventory != null ? playerInventory.Inventory : null;

    private void Awake()
    {
        if (playerInventory == null) playerInventory = GetComponentInChildren<PlayerInventory>(true);
        if (playerInventory == null) playerInventory = GetComponentInParent<PlayerInventory>();
        if (database == null) Debug.LogWarning("[Save] InventorySaveable has no ItemDatabase. Items cannot be loaded.", this);
    }

    protected override SaveData Capture()
    {
        Inventory inv = Inv;
        if (inv == null) return null;   // keep whatever was saved before

        var data = new SaveData();

        // Bags first, so they are placed first on load.
        foreach (bool bags in new[] { true, false })
        {
            foreach (PlacedItem p in inv.Items)
            {
                if (p.IsBag != bags) continue;

                if (database == null || !database.Contains(p.Item))
                    Debug.LogWarning($"[Save] '{p.Item.name}' (id '{p.Item.id}') is not in the ItemDatabase. " +
                                     "It will be lost when this save is loaded.", p.Item);

                data.items.Add(new Entry
                {
                    itemId = p.Item.id,
                    qty = p.Quantity,
                    x = p.Origin.x,
                    y = p.Origin.y,
                    rot = p.Rotation,
                });
            }
        }

        return data;
    }

    protected override void Restore(SaveData state, int version)
    {
        Inventory inv = Inv;
        if (inv == null || database == null) return;

        // Turn ids into assets first, so bags can be placed before everything else.
        var found = new List<(ItemData item, Entry entry)>();
        foreach (Entry e in state.items)
        {
            if (e == null || e.qty <= 0) continue;

            if (database.TryGet(e.itemId, out ItemData item)) found.Add((item, e));
            else Debug.LogWarning($"[Save] Item id '{e.itemId}' is not in the ItemDatabase. Skipped.", this);
        }

        inv.Clear();

        foreach (bool bags in new[] { true, false })
        {
            foreach ((ItemData item, Entry e) in found)
            {
                if (item.isBag == bags) Place(inv, item, e);
            }
        }
    }

    private void Place(Inventory inv, ItemData item, Entry e)
    {
        PlacedItem placed = inv.TryPlace(item, e.qty, new Vector2Int(e.x, e.y), e.rot);
        int left = e.qty - (placed != null ? placed.Quantity : 0);

        // The saved spot is no longer valid (board, shape or max stack changed): put the rest anywhere.
        if (left > 0) left = inv.TryAdd(item, left);
        if (left > 0) Debug.LogWarning($"[Save] {left}x '{item.id}' did not fit in the inventory and was lost.", this);
    }
}
