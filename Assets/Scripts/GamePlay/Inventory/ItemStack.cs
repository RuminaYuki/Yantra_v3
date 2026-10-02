using System;

[Serializable]
public struct ItemStack
{
    public ItemData item;
    public int quantity;

    public ItemStack(ItemData item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    public bool IsEmpty => item == null || quantity <= 0;

    // How many more items fit into this stack
    public int SpaceLeft => IsEmpty ? 0 : item.maxStack - quantity;
}
