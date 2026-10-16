using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    [Tooltip("Unique and never changed after release. Auto-filled from the asset name.")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public ItemCategory category;

    [Header("Stacking")]
    [Tooltip("1 = does not stack. A stack still takes the space of one item.")]
    [Min(1)] public int maxStack = 1;

    [Header("Shape")]
    [Tooltip("Can the player rotate this item while dragging it?")]
    public bool canRotate = true;
    [Tooltip("Cells this item covers. (0,0) is top-left, x goes right, y goes down. Empty = 1x1. " +
             "Edit it with the grid at the bottom of the Inspector.")]
    public List<Vector2Int> shape = new List<Vector2Int> { Vector2Int.zero };

    [Header("Bag")]
    [Tooltip("A bag is placed on the board and its cells become storage space for other items. Bags never stack.")]
    public bool isBag;

    [Header("Actions")]
    [Tooltip("Extra right-click actions for this item only (added after the category actions).")]
    public List<ItemActionSO> extraActions = new List<ItemActionSO>();

    [Header("Prefabs")]
    [Tooltip("Spawned when the item is dropped on the ground.")]
    public WorldItem worldPrefab;
    [Tooltip("Spawned in the hand socket when equipped. Leave empty for non-holdable items.")]
    public HeldItem heldPrefab;

    [Header("Inventory 3D Preview")]
    [Tooltip("Visual-only model shown in the inventory. Leave empty to use worldPrefab.")]
    public GameObject previewPrefab;
    [Tooltip("Starting rotation of the model in the preview.")]
    public Vector3 previewRotation;

    public GameObject PreviewSource =>
        previewPrefab != null ? previewPrefab : (worldPrefab != null ? worldPrefab.gameObject : null);

    // ---------- Shape helpers (rotation 0..3, each step = 90 degrees clockwise) ----------

    [NonSerialized] private Vector2Int[][] cellCache;   // normalized cells per rotation
    [NonSerialized] private Vector2Int[] offsetCache;   // what was subtracted to normalize
    [NonSerialized] private Vector2Int[] sizeCache;     // bounding box per rotation

    public static int WrapRotation(int rotation) => ((rotation % 4) + 4) % 4;

    // Rotates a cell around (0,0). With y pointing down, one step is clockwise on screen.
    public static Vector2Int RotateCell(Vector2Int c, int rotation)
    {
        switch (WrapRotation(rotation))
        {
            case 1: return new Vector2Int(-c.y, c.x);
            case 2: return new Vector2Int(-c.x, -c.y);
            case 3: return new Vector2Int(c.y, -c.x);
            default: return c;
        }
    }

    // Cells covered at this rotation, shifted so the top-left of the bounding box is (0,0).
    public IReadOnlyList<Vector2Int> GetCells(int rotation)
    {
        if (cellCache == null) BuildCache();
        return cellCache[WrapRotation(rotation)];
    }

    public Vector2Int GetSize(int rotation)
    {
        if (cellCache == null) BuildCache();
        return sizeCache[WrapRotation(rotation)];
    }

    // Offset removed from the raw rotated shape. Needed to move items together with a rotating bag.
    public Vector2Int GetRotationOffset(int rotation)
    {
        if (cellCache == null) BuildCache();
        return offsetCache[WrapRotation(rotation)];
    }

    private void BuildCache()
    {
        var raw = new List<Vector2Int>();
        if (shape != null)
            foreach (Vector2Int c in shape)
                if (!raw.Contains(c)) raw.Add(c);
        if (raw.Count == 0) raw.Add(Vector2Int.zero);

        cellCache = new Vector2Int[4][];
        offsetCache = new Vector2Int[4];
        sizeCache = new Vector2Int[4];

        for (int r = 0; r < 4; r++)
        {
            var cells = new Vector2Int[raw.Count];
            Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
            for (int i = 0; i < raw.Count; i++)
            {
                cells[i] = RotateCell(raw[i], r);
                min = Vector2Int.Min(min, cells[i]);
            }

            Vector2Int max = Vector2Int.zero;
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] -= min;
                max = Vector2Int.Max(max, cells[i]);
            }

            cellCache[r] = cells;
            offsetCache[r] = min;
            sizeCache[r] = max + Vector2Int.one;
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
        if (maxStack < 1) maxStack = 1;
        if (isBag) maxStack = 1;
        cellCache = null; // shape may have changed
    }
}
