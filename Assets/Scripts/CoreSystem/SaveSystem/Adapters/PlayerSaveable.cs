using System;
using UnityEngine;

// Saves the player's HP, Ap (skill points) and ammo, and places the player at the
// spawn point after loading. Put it on the player root.
// The player's transform itself is not saved: they appear at the SavePoint they used,
// so they never load inside a wall or in mid-air.
[DisallowMultipleComponent]
public class PlayerSaveable : SaveableBehaviour<PlayerSaveable.SaveData>
{
    [Serializable]
    public class SaveData
    {
        public float hp;
        public float skillPoints;
    }

    [Header("References (empty = found in children)")]
    [SerializeField] private Health health;
    [SerializeField] private SkillPoints skillPoints;
    [SerializeField] private GunController gun;

    [Tooltip("Object moved to the spawn point. Empty = the object with the CharacterController, or this one.")]
    [SerializeField] private Transform moveRoot;

    protected override string DefaultKey => "player";

    private void Awake()
    {
        if (health == null) health = GetComponentInChildren<Health>(true);
        if (skillPoints == null) skillPoints = GetComponentInChildren<SkillPoints>(true);
        if (gun == null) gun = GetComponentInChildren<GunController>(true);

        if (moveRoot == null)
        {
            CharacterController cc = GetComponentInChildren<CharacterController>(true);
            moveRoot = cc != null ? cc.transform : transform;
        }
    }

    protected override void Start()
    {
        base.Start();           // values first
        PlaceAtSpawnPoint();
    }

    protected override SaveData Capture() => new SaveData
    {
        hp = health != null ? health.CurrentHP : 0f,
        skillPoints = skillPoints != null ? skillPoints.CurrentSkillPoints : 0f,
    };

    protected override void Restore(SaveData state, int version)
    {
        if (health != null)
        {
            // A save is never made while dead; treat 0 as broken data and give full HP.
            if (state.hp > 0f)
            {
                health.SetCurrentHealth(state.hp);
                Debug.Log($"[Save] Restored player HP: {state.hp}");
            }
            else
            {
                health.RestoreFullHealth();
                Debug.LogWarning($"[Save] Player HP was 0 or negative; restored full health.");
            }
        }

        if (skillPoints != null) skillPoints.SetCurrent(state.skillPoints);
    }

    // Called by SavePoint just before saving.
    public void Refill(bool fullHealth, bool fullAmmo)
    {
        if (fullHealth && health != null) health.RestoreFullHealth();
    }

    private void PlaceAtSpawnPoint()
    {
        SaveManager manager = SaveManager.Instance;
        if (manager == null) return;

        // New game (no id) or the point is not in this scene: stay where the player was placed.
        if (!SpawnPoint.TryGet(manager.SpawnPointId, out SpawnPoint point)) return;

        Transform target = point.SpawnTransform;
        CharacterController cc = moveRoot.GetComponent<CharacterController>();

        // A CharacterController moves the transform back to where it was if it stays enabled.
        bool ccWasEnabled = cc != null && cc.enabled;
        if (ccWasEnabled) cc.enabled = false;

        moveRoot.SetPositionAndRotation(target.position, target.rotation);

        if (ccWasEnabled) cc.enabled = true;
    }
}
