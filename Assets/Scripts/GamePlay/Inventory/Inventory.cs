using System;
using System.Collections.Generic;
using UnityEngine;

// Pure logic, no MonoBehaviour and no UI knowledge.
// Backpack Battles style board: items have shapes, can rotate and cover several cells.
//
// The board has two layers:
//   - bag layer:  bags sit on cells outside the start area; their cells become storage
//   - item layer: normal items must sit fully on storage cells (start area or a bag)
// Not using bags? Make the start area cover the whole board and the bag layer stays empty.
public class Inventory
{
    public event Action<PlacedItem> ItemAdded;
    public event Action<PlacedItem> ItemRemoved;
    public event Action<PlacedItem> ItemChanged;   // quantity, position or rotation changed
    public event Action CellsChanged;              // storage area changed (bag added, moved or removed)

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<PlacedItem> Items => items;

    private readonly List<PlacedItem> items = new List<PlacedItem>();
    private readonly bool[,] startCells;
    private readonly PlacedItem[,] bagLayer;
    private readonly PlacedItem[,] itemLayer;

    private struct Move
    {
        public PlacedItem Item;
        public Vector2Int Origin;
        public int Rotation;
    }

    public Inventory(int width, int height, RectInt startArea)
    {
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        startCells = new bool[Width, Height];
        bagLayer = new PlacedItem[Width, Height];
        itemLayer = new PlacedItem[Width, Height];

        for (int x = startArea.xMin; x < startArea.xMax; x++)
            for (int y = startArea.yMin; y < startArea.yMax; y++)
                if (InBounds(new Vector2Int(x, y))) startCells[x, y] = true;
    }

    // ---------- Queries ----------

    public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

    // True if a normal item can sit on this cell.
    public bool IsStorageCell(Vector2Int c) =>
        InBounds(c) && (startCells[c.x, c.y] || bagLayer[c.x, c.y] != null);

    public PlacedItem GetItemAt(Vector2Int c) => InBounds(c) ? itemLayer[c.x, c.y] : null;
    public PlacedItem GetBagAt(Vector2Int c) => InBounds(c) ? bagLayer[c.x, c.y] : null;

    public bool Contains(PlacedItem p) => p != null && items.Contains(p);

    public bool CanPlace(ItemData item, Vector2Int origin, int rotation) =>
        item != null && (item.canRotate || ItemData.WrapRotation(rotation) == 0) &&
        CanOccupy(item, origin, rotation, null);

    // A bag with items on it cannot be removed, otherwise those items would float.
    public bool CanRemove(PlacedItem p) => Contains(p) && (!p.IsBag || GetItemsOnBag(p).Count == 0);

    public List<PlacedItem> GetItemsOnBag(PlacedItem bag)
    {
        var result = new List<PlacedItem>();
        if (bag == null || !bag.IsBag) return result;
        foreach (Vector2Int c in bag.Cells)
        {
            PlacedItem o = GetItemAt(c);
            if (o != null && !result.Contains(o)) result.Add(o);
        }
        return result;
    }

    // First free spot, trying every allowed rotation. Used for pickups.
    public bool FindSpace(ItemData item, out Vector2Int origin, out int rotation)
    {
        int rotations = item.canRotate ? 4 : 1;
        for (int r = 0; r < rotations; r++)
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var o = new Vector2Int(x, y);
                    if (!CanOccupy(item, o, r, null)) continue;
                    origin = o;
                    rotation = r;
                    return true;
                }

        origin = default;
        rotation = 0;
        return false;
    }

    // ---------- Add / remove ----------

    // Returns the amount that did NOT fit (0 means everything was added).
    public int TryAdd(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return amount;

        // Pass 1: top up existing stacks of the same item
        foreach (PlacedItem p in items)
        {
            if (amount <= 0) break;
            if (p.Item != item) continue;
            int add = Math.Min(amount, p.SpaceLeft);
            if (add <= 0) continue;
            p.Quantity += add;
            amount -= add;
            ItemChanged?.Invoke(p);
        }

        // Pass 2: place new stacks wherever the shape fits
        while (amount > 0 && FindSpace(item, out Vector2Int origin, out int rotation))
        {
            PlacedItem p = TryPlace(item, amount, origin, rotation);
            if (p == null) break;
            amount -= p.Quantity;
        }

        return amount;
    }

    // Puts a new stack at an exact spot (also what a save system would call on load).
    // Returns null if it does not fit. Quantity above maxStack is cut off.
    public PlacedItem TryPlace(ItemData item, int quantity, Vector2Int origin, int rotation)
    {
        if (item == null || quantity <= 0) return null;
        rotation = ItemData.WrapRotation(rotation);
        if (!CanPlace(item, origin, rotation)) return null;

        var p = new PlacedItem(item, Math.Min(quantity, item.maxStack), origin, rotation);
        items.Add(p);
        Write(p, p);

        ItemAdded?.Invoke(p);
        if (p.IsBag) CellsChanged?.Invoke();
        return p;
    }

    // Returns how many were actually removed.
    public int TryRemove(PlacedItem p, int amount)
    {
        if (!CanRemove(p) || amount <= 0) return 0;

        int removed = Math.Min(amount, p.Quantity);
        p.Quantity -= removed;
        if (p.Quantity <= 0) Delete(p);
        else ItemChanged?.Invoke(p);
        return removed;
    }

    public void Clear()
    {
        var old = new List<PlacedItem>(items);
        items.Clear();
        Array.Clear(bagLayer, 0, bagLayer.Length);
        Array.Clear(itemLayer, 0, itemLayer.Length);
        foreach (PlacedItem p in old) ItemRemoved?.Invoke(p);
        CellsChanged?.Invoke();
    }

    // ---------- Move / merge ----------

    // Same check as TryMove without changing anything (for the drag preview).
    public bool CanMove(PlacedItem p, Vector2Int origin, int rotation) => BuildMove(p, origin, rotation, null);

    // Moving a bag also moves and rotates every item resting on it.
    public bool TryMove(PlacedItem p, Vector2Int origin, int rotation)
    {
        rotation = ItemData.WrapRotation(rotation);
        if (Contains(p) && p.Origin == origin && p.Rotation == rotation) return true; // dropped in place

        var plan = new List<Move>();
        if (!BuildMove(p, origin, rotation, plan)) return false;

        foreach (Move m in plan) Write(m.Item, null);
        foreach (Move m in plan)
        {
            m.Item.Origin = m.Origin;
            m.Item.Rotation = m.Rotation;
            Write(m.Item, m.Item);
        }

        foreach (Move m in plan) ItemChanged?.Invoke(m.Item);
        if (p.IsBag) CellsChanged?.Invoke();
        return true;
    }

    // Moves as much as fits from one stack into another of the same item.
    public bool TryMerge(PlacedItem from, PlacedItem into)
    {
        if (!Contains(from) || !Contains(into) || from == into || from.Item != into.Item) return false;

        int moved = Math.Min(into.SpaceLeft, from.Quantity);
        if (moved <= 0) return false;

        into.Quantity += moved;
        from.Quantity -= moved;
        ItemChanged?.Invoke(into);
        if (from.Quantity <= 0) Delete(from);
        else ItemChanged?.Invoke(from);
        return true;
    }

    // ---------- Internals ----------

    // ignore = an item already on the board that is being moved (its own cells count as free).
    private bool CanOccupy(ItemData item, Vector2Int origin, int rotation, PlacedItem ignore)
    {
        foreach (Vector2Int local in item.GetCells(rotation))
        {
            Vector2Int c = origin + local;
            if (!InBounds(c)) return false;

            if (item.isBag)
            {
                if (startCells[c.x, c.y]) return false;
                PlacedItem b = bagLayer[c.x, c.y];
                if (b != null && b != ignore) return false;
            }
            else
            {
                if (!IsStorageCell(c)) return false;
                PlacedItem o = itemLayer[c.x, c.y];
                if (o != null && o != ignore) return false;
            }
        }
        return true;
    }

    private bool BuildMove(PlacedItem p, Vector2Int origin, int rotation, List<Move> plan)
    {
        if (!Contains(p)) return false;
        rotation = ItemData.WrapRotation(rotation);
        if (rotation != p.Rotation && !p.Item.canRotate) return false;

        if (!p.IsBag)
        {
            if (!CanOccupy(p.Item, origin, rotation, p)) return false;
            plan?.Add(new Move { Item = p, Origin = origin, Rotation = rotation });
            return true;
        }

        // Bag: its new cells must be free board cells outside the start area.
        var newBagCells = new HashSet<Vector2Int>();
        foreach (Vector2Int local in p.Item.GetCells(rotation))
        {
            Vector2Int c = origin + local;
            if (!InBounds(c) || startCells[c.x, c.y]) return false;
            PlacedItem b = bagLayer[c.x, c.y];
            if (b != null && b != p) return false;
            newBagCells.Add(c);
        }

        // Items resting on the bag come along, keeping their place relative to the bag.
        List<PlacedItem> carried = GetItemsOnBag(p);
        var carriedSet = new HashSet<PlacedItem>(carried);
        int turn = ItemData.WrapRotation(rotation - p.Rotation);
        var moves = new List<Move> { new Move { Item = p, Origin = origin, Rotation = rotation } };

        foreach (PlacedItem it in carried)
        {
            if (turn != 0 && !it.Item.canRotate) return false;

            Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
            foreach (Vector2Int c in it.Cells)
            {
                Vector2Int n = MoveWithBag(p, c, origin, rotation);
                if (!InBounds(n)) return false;

                PlacedItem otherBag = bagLayer[n.x, n.y];
                bool storage = startCells[n.x, n.y] || newBagCells.Contains(n) || (otherBag != null && otherBag != p);
                if (!storage) return false;

                PlacedItem o = itemLayer[n.x, n.y];
                if (o != null && !carriedSet.Contains(o)) return false;

                min = Vector2Int.Min(min, n);
            }

            moves.Add(new Move { Item = it, Origin = min, Rotation = ItemData.WrapRotation(it.Rotation + turn) });
        }

        plan?.AddRange(moves);
        return true;
    }

    // Where a board cell ends up when the bag moves to newOrigin / newRotation.
    private static Vector2Int MoveWithBag(PlacedItem bag, Vector2Int cell, Vector2Int newOrigin, int newRotation)
    {
        // Back into the bag's unrotated local space, then out again with the new placement.
        Vector2Int local = ItemData.RotateCell(cell - bag.Origin + bag.Item.GetRotationOffset(bag.Rotation), -bag.Rotation);
        return ItemData.RotateCell(local, newRotation) - bag.Item.GetRotationOffset(newRotation) + newOrigin;
    }

    private void Write(PlacedItem p, PlacedItem value)
    {
        PlacedItem[,] layer = p.IsBag ? bagLayer : itemLayer;
        foreach (Vector2Int c in p.Cells)
            if (InBounds(c)) layer[c.x, c.y] = value;
    }

    private void Delete(PlacedItem p)
    {
        Write(p, null);
        items.Remove(p);
        ItemRemoved?.Invoke(p);
        if (p.IsBag) CellsChanged?.Invoke();
    }
}
