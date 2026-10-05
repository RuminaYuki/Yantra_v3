using UnityEngine;
using UnityEngine.InputSystem;

// Owns the Inventory and connects it to the game world:
// pickup, drop and equip.
public class PlayerInventory : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private InputActionReference inputAction;
    [SerializeField] private InventoryUI ui;
    [Tooltip("Where dropped items spawn. Leave empty to spawn in front of this object.")]
    [SerializeField] private Transform dropPoint;

    [Header("Setting")]
    [SerializeField] private int slotCount = 20;
    [SerializeField] private float dropForwardDistance = 1f;
    [SerializeField] private float dropUpOffset = 0.5f;

    public Inventory Inventory { get; private set; }

    private void Awake()
    {
        Inventory = new Inventory(slotCount);
    }

    private void OnEnable()
    {
        if (inputAction != null)
        {
            inputAction.action.started += HandleOpenInventory;
        }
    }

    private void OnDisable()
    {
        if (inputAction != null)
        {
            inputAction.action.started -= HandleOpenInventory;
        }
    }

    private void HandleOpenInventory(InputAction.CallbackContext context)
    {
        if (ui != null)
        {
            ui.Toggle();
        }
    }

    // Picks up as much as fits. The world item keeps whatever did not fit,
    // so its quantity becomes 0 when everything was taken.
    // Returns true if at least one item was picked up.
    public bool TryPickup(WorldItem worldItem)
    {
        ItemStack s = worldItem.Stack;
        if (s.IsEmpty) return false;

        int left = Inventory.TryAdd(s.item, s.quantity);
        worldItem.SetQuantity(left);
        return left < s.quantity;
    }

    // Removes items from a slot and spawns them on the ground as one stack.
    public void DropFromSlot(int index, int amount)
    {
        if (!Inventory.IsValid(index)) return;

        ItemStack s = Inventory[index];
        if (s.IsEmpty || s.item.worldPrefab == null) return;

        int removed = Inventory.TryRemoveAt(index, amount);
        if (removed <= 0) return;

        Vector3 pos;
        Quaternion rot;
        if (dropPoint != null)
        {
            pos = dropPoint.position;
            rot = dropPoint.rotation;
        }
        else
        {
            pos = transform.position + transform.forward * dropForwardDistance + Vector3.up * dropUpOffset;
            rot = Quaternion.LookRotation(transform.forward, Vector3.up);
        }

        WorldItem dropped = Instantiate(s.item.worldPrefab, pos, rot);
        dropped.Initialize(new ItemStack(s.item, removed));
    }
}
