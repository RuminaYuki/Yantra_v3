using UnityEngine;

// Everything the right-click menu needs to run an action on one slot.
public readonly struct ItemActionContext
{
    public readonly PlayerInventory Player;
    public readonly InventoryUI UI;
    public readonly int SlotIndex;

    public ItemActionContext(PlayerInventory player, InventoryUI ui, int slotIndex)
    {
        Player = player;
        UI = ui;
        SlotIndex = slotIndex;
    }

    public ItemStack Stack => Player.Inventory[SlotIndex];
}

// Base class for anything that shows up in the right-click menu.
// To add a new action (Use, Equip, Eat...): inherit, override Execute,
// create the asset, then add it to a category, an item, or the global list in InventoryUI.
public abstract class ItemActionSO : ScriptableObject
{
    [SerializeField] private string label;

    public string Label => string.IsNullOrEmpty(label) ? name : label;

    // Return false to hide the action for this slot.
    public virtual bool CanExecute(in ItemActionContext ctx) => !ctx.Stack.IsEmpty;

    public abstract void Execute(in ItemActionContext ctx);
}
