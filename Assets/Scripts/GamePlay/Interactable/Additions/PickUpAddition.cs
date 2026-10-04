using SDFcl.GamePlay.Interactable;
using UnityEngine;

public class PickUpAddition : MonoBehaviour
{
    [SerializeField] WorldItem worldItem;

    Iinteractor interactor;

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

    private void HandleOnInteract(GameObject rootplayer)
    {
        PlayerInventory inventory = rootplayer.GetComponentInChildren<PlayerInventory>();
        if (inventory == null) return;

        inventory.TryPickup(worldItem);
    }

    private void HandleOnEndInteract(GameObject rootplayer)
    {
        Destroy(worldItem.gameObject, 0.01f);
    }
}
