using System.Collections;
using SDFcl.GamePlay.Interactable;
using UnityEngine;

// The only place the player can save (GDD: "Player can only save at designated spot").
// Put it on an object that has an interactor (e.g. ButtonInteractor), like PickUpAddition.
// Interacting saves the game, writes the file and reloads the scene, so normal enemies come back.
public class SavePoint : SpawnPoint
{
    [Tooltip("Place shown on the Load Save screen (name + background image).")]
    [SerializeField] private LocationData location;

    [Header("On save")]
    [SerializeField] private bool restoreHealth = true;
    [SerializeField] private bool restoreAmmo = true;

    [Tooltip("Reload the scene after saving so normal enemies respawn.")]
    [SerializeField] private bool reloadScene = true;

    public LocationData Location => location;

    protected override void Awake()
    {
        base.Awake();
        if (location == null)
            Debug.LogWarning($"[Save] SavePoint on {name} has no LocationData. The Load Save screen will show the fallback image.", this);
    }

    public void HandleSave(GameObject rootPlayer)
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
    }
}
