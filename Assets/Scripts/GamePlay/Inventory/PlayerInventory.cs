using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Owns the Inventory and connects it to the game world:
// pickup, drop and equip.
public class PlayerInventory : MonoBehaviour
{
    [Header("Referecn")]
    [SerializeField] private InputActionReference inputAction;
    [SerializeField] private InventoryUI ui;

    [Header("Setting")]
    [SerializeField] private int slotCount = 20;

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

    public bool TryPickup(WorldItem worldItem)
    {
        ItemStack s = worldItem.Stack;
        if (s.IsEmpty) return false;

        int left = Inventory.TryAdd(s.item, s.quantity);

        Debug.Log("Here");

        if (left <= 0)
        {
            Destroy(worldItem.gameObject);
            return true;
        }

        // Inventory was full: leave the remainder on the ground
        worldItem.SetQuantity(left);
        return left < s.quantity;
    }
}
