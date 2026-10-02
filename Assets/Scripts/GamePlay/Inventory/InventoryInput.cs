using UnityEngine;

// All input lives here so it is easy to swap to the new Input System later.
// Uses the legacy Input Manager (UnityEngine.Input).
public class InventoryInput : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private InventoryUI ui;
    [SerializeField] private float pickupRadius = 2f;
    [SerializeField] private LayerMask pickupMask = ~0;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) ui.Toggle();
        if (ui.IsOpen) return;

        if (Input.GetKeyDown(KeyCode.E)) TryPickupNearest();
        if (Input.GetKeyDown(KeyCode.G) && inventory.EquippedSlot >= 0)
            inventory.DropFromSlot(inventory.EquippedSlot, 1);

        // Keys 1-9 equip / unequip the matching slot
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) inventory.ToggleEquip(i);
        }
    }

    private void TryPickupNearest()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, pickupRadius, pickupMask, QueryTriggerInteraction.Collide);

        WorldItem best = null;
        float bestDist = float.MaxValue;
        foreach (Collider c in hits)
        {
            WorldItem w = c.GetComponentInParent<WorldItem>();
            if (w == null) continue;
            float d = (w.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = w; }
        }

        if (best != null) inventory.TryPickup(best);
    }
}
