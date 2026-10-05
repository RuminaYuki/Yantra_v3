using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Actions/Drop")]
public class DropItemActionSO : ItemActionSO
{
    // Hidden when the item has no world prefab, otherwise it would just vanish.
    public override bool CanExecute(in ItemActionContext ctx)
    {
        ItemStack s = ctx.Stack;
        return !s.IsEmpty && s.item.worldPrefab != null;
    }

    public override void Execute(in ItemActionContext ctx)
    {
        ItemStack s = ctx.Stack;
        PlayerInventory player = ctx.Player;
        int slot = ctx.SlotIndex;

        if (s.quantity <= 1)
        {
            player.DropFromSlot(slot, 1);
            return;
        }

        ctx.UI.RequestAmount(Label, s.item, s.quantity, amount => player.DropFromSlot(slot, amount));
    }
}
