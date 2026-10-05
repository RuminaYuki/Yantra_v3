using System.Collections.Generic;
using UnityEngine;

// A category is an asset, so designers can add new ones in the Editor
// without touching code (Create > Inventory/Category).
// Every item in this category gets the actions listed here.
[CreateAssetMenu(menuName = "Inventory/Category")]
public class ItemCategory : ScriptableObject
{
    public string displayName;
    public Sprite icon;

    [Tooltip("Actions shown in the right-click menu for every item in this category.")]
    public List<ItemActionSO> actions = new List<ItemActionSO>();

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
}
