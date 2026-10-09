using System.Collections.Generic;
using UnityEngine;

// One item (or stack, or bag) lying on the inventory board.
// Only Inventory changes it; everyone else just reads.
public class PlacedItem
{
    public ItemData Item { get; }
    public int Quantity { get; internal set; }
    public Vector2Int Origin { get; internal set; }   // top-left cell of the bounding box
    public int Rotation { get; internal set; }        // 0..3, 90 degrees clockwise per step

    internal PlacedItem(ItemData item, int quantity, Vector2Int origin, int rotation)
    {
        Item = item;
        Quantity = quantity;
        Origin = origin;
        Rotation = rotation;
    }

    public bool IsBag => Item.isBag;
    public int SpaceLeft => Item.maxStack - Quantity;
    public Vector2Int Size => Item.GetSize(Rotation);
    public ItemStack Stack => new ItemStack(Item, Quantity);

    // Board cells this item covers right now.
    public IEnumerable<Vector2Int> Cells
    {
        get
        {
            foreach (Vector2Int c in Item.GetCells(Rotation)) yield return Origin + c;
        }
    }
}
