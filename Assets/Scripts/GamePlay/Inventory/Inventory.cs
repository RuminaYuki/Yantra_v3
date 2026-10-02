using System;

// Pure logic, no MonoBehaviour and no UI knowledge.
// The UI listens to OnSlotChanged and redraws only the slot that changed.
public class Inventory
{
    public event Action<int> OnSlotChanged;

    private readonly ItemStack[] slots;

    public int SlotCount => slots.Length;
    public ItemStack this[int index] => slots[index];

    public Inventory(int slotCount)
    {
        slots = new ItemStack[slotCount];
    }

    public bool IsValid(int index) => index >= 0 && index < slots.Length;

    // Returns the amount that did NOT fit (0 means everything was added).
    public int TryAdd(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return amount;

        // Pass 1: top up existing stacks of the same item
        for (int i = 0; i < slots.Length && amount > 0; i++)
        {
            if (slots[i].IsEmpty || slots[i].item != item) continue;
            int add = Math.Min(amount, slots[i].SpaceLeft);
            if (add <= 0) continue;
            slots[i].quantity += add;
            amount -= add;
            OnSlotChanged?.Invoke(i);
        }

        // Pass 2: fill empty slots
        for (int i = 0; i < slots.Length && amount > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;
            int add = Math.Min(amount, item.maxStack);
            slots[i] = new ItemStack(item, add);
            amount -= add;
            OnSlotChanged?.Invoke(i);
        }

        return amount;
    }

    // Returns how many items were actually removed.
    public int TryRemoveAt(int index, int amount)
    {
        if (!IsValid(index) || slots[index].IsEmpty || amount <= 0) return 0;

        int removed = Math.Min(amount, slots[index].quantity);
        slots[index].quantity -= removed;
        if (slots[index].quantity <= 0) slots[index] = default;
        OnSlotChanged?.Invoke(index);
        return removed;
    }

    public void Swap(int a, int b)
    {
        if (!IsValid(a) || !IsValid(b) || a == b) return;
        (slots[a], slots[b]) = (slots[b], slots[a]);
        OnSlotChanged?.Invoke(a);
        OnSlotChanged?.Invoke(b);
    }

    // Same item: merge as much as fits. Different item or empty target: swap.
    public void Move(int from, int to)
    {
        if (!IsValid(from) || !IsValid(to) || from == to || slots[from].IsEmpty) return;

        if (!slots[to].IsEmpty && slots[to].item == slots[from].item)
        {
            int moved = Math.Min(slots[to].SpaceLeft, slots[from].quantity);
            if (moved <= 0) return;
            slots[to].quantity += moved;
            slots[from].quantity -= moved;
            if (slots[from].quantity <= 0) slots[from] = default;
            OnSlotChanged?.Invoke(from);
            OnSlotChanged?.Invoke(to);
        }
        else
        {
            Swap(from, to);
        }
    }
}
