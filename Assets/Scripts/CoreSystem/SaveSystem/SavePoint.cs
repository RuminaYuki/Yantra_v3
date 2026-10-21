using System.Collections;
using SDFcl.GamePlay.Interactable;
using UnityEngine;

// The only place the player can save (GDD: "Player can only save at designated spot").
// Put it on an object that has an interactor (e.g. ButtonInteractor), like PickUpAddition.
// Interacting saves the game, writes the file and reloads the scene, so normal enemies come back.
public class SavePoint : SpawnPoint
{
    [Header("On save")]
    [SerializeField] private bool restoreHealth = true;
    [SerializeField] private bool restoreAmmo = true;

    [Tooltip("Reload the scene after saving so normal enemies respawn.")]
    [SerializeField] private bool reloadScene = true;

    private Iinteractor interactor;

    protected override void Awake()
    {
        base.Awake();
        interactor = GetComponent<Iinteractor>();
        if (interactor == null)
            Debug.LogWarning($"[Save] SavePoint on {name} needs an interactor component on the same object.", this);
    }

    private void OnEnable()
    {
        if (interactor != null) interactor.OnInteract += HandleInteract;
    }

    private void OnDisable()
    {
        if (interactor != null) interactor.OnInteract -= HandleInteract;
    }

    private void HandleInteract(GameObject rootPlayer)
    {
        SaveManager manager = SaveManager.Instance;
        bool saved = false;

        if (manager != null && manager.CanSave)
        {
            PlayerSaveable player = rootPlayer != null ? rootPlayer.GetComponentInChildren<PlayerSaveable>() : null;
            if (player != null) player.Refill(restoreHealth, restoreAmmo);
            else Debug.LogWarning("[Save] No PlayerSaveable found on the player.", this);

            saved = manager.SaveAt(this, reloadScene);
        }

        // Scene is reloading: the player is about to be replaced, nothing to end.
        if (saved && reloadScene) return;

        // Otherwise end the interaction so the player is not stuck in it.
        // Wait one frame: the interactor marks the player as interacting AFTER OnInteract returns.
        if (rootPlayer != null) StartCoroutine(EndInteractNextFrame(rootPlayer));
    }

    private IEnumerator EndInteractNextFrame(GameObject rootPlayer)
    {
        yield return null;
        if (interactor != null && rootPlayer != null) interactor.CancelInteraction(rootPlayer, true);
    }
}
