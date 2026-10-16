using SDFcl.GamePlay.Interactable;
using UnityEngine;

public class PickUpAddition : MonoBehaviour
{
    [SerializeField] WorldItem worldItem;
    [SerializeField] float DelayPickup = 0.5f;

    private bool isPickedUp = false;

    Iinteractor interactor;
    PlayerInventory inventory;

    private void Awake()
    {
        interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        interactor.OnInteract += HandleOnInteract;
        interactor.OnEndInteract += HandleOnEndInteract;
    }

    private void OnDisable()
    {
        interactor.OnInteract -= HandleOnInteract;
        interactor.OnEndInteract -= HandleOnEndInteract;
    }

    private void Update()
    {
        if (isPickedUp)
        {
            DelayPickup -= Time.deltaTime;
            if (DelayPickup <= 0f)
            {
                if (inventory == null) return;
                inventory.TryPickup(worldItem);
            }
        }
    }

    private void HandleOnInteract(GameObject rootplayer)
    {
        inventory = rootplayer.GetComponentInChildren<PlayerInventory>();
        isPickedUp = true;
    }

    private void HandleOnEndInteract(GameObject rootplayer)
    {
        // Only remove the world object when everything was picked up.
        // If the inventory was full, the remainder stays on the ground.
        if (worldItem.Stack.IsEmpty)
        {
            isPickedUp = false;
            Destroy(worldItem.gameObject, 0.01f);
        }
    }
}
