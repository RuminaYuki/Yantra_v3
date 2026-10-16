using System;
using SDFcl.GamePlay.Interactable;
using UnityEngine;
using UnityEngine.Events;

// Item requirement check, same idea as state machine condition groups:
//   - between groups = OR  (checked top to bottom, first met group wins)
//   - inside a group = AND (every item must be in the inventory)
// No groups set = no requirement.
public class CheckItemAddition : MonoBehaviour
{
    [Serializable]
    public struct ItemRequirement
    {
        public ItemData Item;
        [Min(1)] public int Amount;
        [Tooltip("Remove this item from the inventory when the interact succeeds.")]
        public bool Consume;

        public int RequiredAmount => Mathf.Max(1, Amount);
    }

    [Serializable]
    public class ItemRequirementGroup
    {
        public string Label;
        public ItemRequirement[] Items = Array.Empty<ItemRequirement>();
        [Tooltip("Called with rootPlayer when this group was used to interact.")]
        public UnityEvent<GameObject> OnUsed;
    }

    [SerializeField] ItemRequirementGroup[] _groups = Array.Empty<ItemRequirementGroup>();

    Iinteractor _interactor;

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        _interactor.AddCanInteractModifier(CanInteract);
        _interactor.OnInteract += HandleOnInteract;
    }

    private void OnDisable()
    {
        _interactor.RemoveCanInteractModifier(CanInteract);
        _interactor.OnInteract -= HandleOnInteract;
    }

    private bool CanInteract(GameObject rootPlayer)
    {
        if (_groups == null || _groups.Length == 0) return true;

        BaseInteractor baseInteractor = _interactor as BaseInteractor;
        baseInteractor.SetCanInteract(FindMetGroup(rootPlayer, out _) >= 0);

        return true;
    }

    private void HandleOnInteract(GameObject rootPlayer)
    {
        // Check again: the inventory may have changed since CanInteract.
        int index = FindMetGroup(rootPlayer, out Inventory inventory);
        if (index < 0) return;

        ItemRequirementGroup group = _groups[index];
        foreach (ItemRequirement requirement in group.Items)
        {
            if (requirement.Consume)
                inventory.TryRemove(requirement.Item, requirement.RequiredAmount);
        }

        group.OnUsed?.Invoke(rootPlayer);
    }

    // Returns the index of the first group whose items are all in the inventory, or -1.
    private int FindMetGroup(GameObject rootPlayer, out Inventory inventory)
    {
        inventory = null;
        if (rootPlayer == null || _groups == null) return -1;

        PlayerInventory playerInventory = rootPlayer.GetComponentInChildren<PlayerInventory>();
        if (playerInventory == null || playerInventory.Inventory == null) return -1;
        inventory = playerInventory.Inventory;

        for (int i = 0; i < _groups.Length; i++)
        {
            if (AreAllMet(inventory, _groups[i])) return i;
        }
        return -1;
    }

    private static bool AreAllMet(Inventory inventory, ItemRequirementGroup group)
    {
        // Empty group never passes, same as StateTransition.
        if (group == null || group.Items == null || group.Items.Length == 0) return false;

        foreach (ItemRequirement requirement in group.Items)
        {
            if (requirement.Item == null) return false;
            if (inventory.CountOf(requirement.Item) < requirement.RequiredAmount) return false;
        }
        return true;
    }
}
