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
    [Min(1)] public int maxStack = 1;

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

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
        if (maxStack < 1) maxStack = 1;
    }
}
