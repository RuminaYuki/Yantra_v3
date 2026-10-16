using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Actions/Drop")]
public class DropItemActionSO : ItemActionSO
{
    // Hidden when the item has no world prefab (it would just vanish)
    // and for a bag that still has items on it.
    public override bool CanExecute(in ItemActionContext ctx)
    {
        ItemStack s = ctx.Stack;
        return !s.IsEmpty && s.item.worldPrefab != null && ctx.Player.Inventory.CanRemove(ctx.Target);
    }

    public override void Execute(in ItemActionContext ctx)
    {
        ItemStack s = ctx.Stack;
        PlayerInventory player = ctx.Player;
        PlacedItem target = ctx.Target;

        if (s.quantity <= 1)
        {
            player.DropItem(target, 1);
            return;
        }

        ctx.UI.RequestAmount(Label, s.item, s.quantity, amount => player.DropItem(target, amount));
    }
}
