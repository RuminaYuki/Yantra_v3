using UnityEngine;

// Item lying on the ground. Needs a Collider (child is fine) and a Rigidbody.
[RequireComponent(typeof(Rigidbody))]
public class WorldItem : MonoBehaviour
{
    [SerializeField] private ItemStack stack;

    public ItemStack Stack => stack;

    public void Initialize(ItemStack newStack) => stack = newStack;

    // Used when the player could only pick up part of the stack
    public void SetQuantity(int quantity) => stack.quantity = quantity;
}
