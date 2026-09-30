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

    [Header("Prefabs")]
    [Tooltip("Spawned when the item is dropped on the ground.")]
    public WorldItem worldPrefab;
    [Tooltip("Spawned in the hand socket when equipped. Leave empty for non-holdable items.")]
    public HeldItem heldPrefab;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
        if (maxStack < 1) maxStack = 1;
    }
}
