using System.Collections;
using SDFcl.GamePlay.Interactable;
using UnityEngine;

public class PickUpAddition : MonoBehaviour
{
    [SerializeField] WorldItem worldItem;
    [SerializeField] float DelayPickup = 0.5f;

    private bool isPickedUp = false;

    Iinteractor interactor;
    PlayerInventory inventory;
    Coroutine Coroutine;

    private void Awake()
    {
        interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        interactor.AddCanInteractModifier(HandleCanInteract);
        interactor.OnInteract += HandleOnInteract;
        interactor.OnEndInteract += HandleOnEndInteract;
    }

    private void OnDisable()
    {
        interactor.RemoveCanInteractModifier(HandleCanInteract);
        interactor.OnInteract -= HandleOnInteract;
        interactor.OnEndInteract -= HandleOnEndInteract;
    }
    private bool HandleCanInteract(GameObject rootplayer)
    {
        if (worldItem == null || worldItem.Stack.IsEmpty) return false;

        inventory = rootplayer.GetComponentInChildren<PlayerInventory>();
        return inventory != null;
    }

    private void HandleOnInteract(GameObject rootplayer)
    {
        if (Coroutine != null)
        {
            return;
        }

        Coroutine = StartCoroutine(DelayPickupCoroutine());
    }

    private void HandleOnEndInteract(GameObject rootplayer)
    {
        // Only remove the world object when everything was picked up.
        // If the inventory was full, the remainder stays on the ground.
        if (worldItem.Stack.IsEmpty)
        {
            Destroy(worldItem.gameObject, 0.01f);
        }
    }

    IEnumerator DelayPickupCoroutine()
    {
        yield return new WaitForSeconds(DelayPickup);
        Coroutine = null; // allow picking up the rest later if the inventory was full

        if (inventory == null) yield break;
        inventory.TryPickup(worldItem);

        // OnEndInteract can fire before this delay ends, so check again here.
        if (worldItem.Stack.IsEmpty)
        {
            Destroy(worldItem.gameObject, 0.01f);
        }
    }
}
