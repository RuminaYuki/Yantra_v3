using System;
using UnityEngine;

// Owns the Inventory and connects it to the game world:
// pickup, drop and equip.
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private int slotCount = 20;
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Transform handSocket;
    [SerializeField] private float dropForce = 3f;

    public Inventory Inventory { get; private set; }
    public int EquippedSlot { get; private set; } = -1;
    public HeldItem CurrentHeld { get; private set; }

    // Argument = equipped slot index, or -1 when nothing is equipped
    public event Action<int> OnEquippedChanged;

    private void Awake()
    {
        Inventory = new Inventory(slotCount);
        Inventory.OnSlotChanged += HandleSlotChanged;
    }

    private void OnDestroy()
    {
        if (Inventory != null) Inventory.OnSlotChanged -= HandleSlotChanged;
    }

    // ---------- Pickup ----------

    public bool TryPickup(WorldItem worldItem)
    {
        ItemStack s = worldItem.Stack;
        if (s.IsEmpty) return false;

        int left = Inventory.TryAdd(s.item, s.quantity);
        if (left <= 0)
        {
            Destroy(worldItem.gameObject);
            return true;
        }

        // Inventory was full: leave the remainder on the ground
        worldItem.SetQuantity(left);
        return left < s.quantity;
    }

    // ---------- Drop ----------

    public void DropFromSlot(int index, int amount)
    {
        if (!Inventory.IsValid(index) || Inventory[index].IsEmpty) return;

        ItemData item = Inventory[index].item;
        if (item.worldPrefab == null)
        {
            Debug.LogWarning($"{item.name} has no worldPrefab, cannot drop.");
            return;
        }

        int removed = Inventory.TryRemoveAt(index, amount);
        if (removed <= 0) return;

        WorldItem dropped = Instantiate(item.worldPrefab, dropPoint.position, dropPoint.rotation);
        dropped.Initialize(new ItemStack(item, removed));
        if (dropped.TryGetComponent(out Rigidbody rb))
            rb.AddForce(dropPoint.forward * dropForce, ForceMode.VelocityChange);
    }

    // ---------- Equip ----------

    // Equipping the already equipped slot unequips it.
    public void ToggleEquip(int index)
    {
        if (index == EquippedSlot) { Unequip(); return; }
        Equip(index);
    }

    public void Equip(int index)
    {
        Unequip();
        if (!Inventory.IsValid(index)) return;

        ItemStack s = Inventory[index];
        if (s.IsEmpty || s.item.heldPrefab == null) return;

        CurrentHeld = Instantiate(s.item.heldPrefab, handSocket);
        CurrentHeld.transform.localPosition = Vector3.zero;
        CurrentHeld.transform.localRotation = Quaternion.identity;
        CurrentHeld.Initialize(s.item);

        EquippedSlot = index;
        CurrentHeld.OnEquip();
        OnEquippedChanged?.Invoke(EquippedSlot);
    }

    public void Unequip()
    {
        if (CurrentHeld != null)
        {
            CurrentHeld.OnUnequip();
            Destroy(CurrentHeld.gameObject);
            CurrentHeld = null;
        }

        if (EquippedSlot != -1)
        {
            EquippedSlot = -1;
            OnEquippedChanged?.Invoke(-1);
        }
    }

    public void UseEquipped()
    {
        if (CurrentHeld != null) CurrentHeld.Use();
    }

    // Keep the held object in sync when the equipped slot changes
    // (item dropped, moved or swapped).
    private void HandleSlotChanged(int index)
    {
        if (index != EquippedSlot) return;

        ItemStack s = Inventory[index];
        if (s.IsEmpty)
            Unequip();
        else if (CurrentHeld == null || CurrentHeld.Data != s.item)
            Equip(index);
    }
}
