using UnityEngine;

// Base class for the version of an item held in hand.
// Inherit for specific behavior (HeldWeapon, HeldConsumable, ...).
// Can also be used as-is on prefabs that only need to be visible in hand.
public class HeldItem : MonoBehaviour
{
    public ItemData Data { get; private set; }

    public void Initialize(ItemData data) => Data = data;

    public virtual void OnEquip() { }
    public virtual void OnUnequip() { }
    public virtual void Use() { }
}
