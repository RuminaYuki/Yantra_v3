using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

public class GunController : MonoBehaviour
{
    public enum GunMode
    {
        Normal,
        Special
    }

    [Header("References")]
    [SerializeField] GameObject PlayerController;
    [SerializeField] SkillPoints skillPoints;
    [SerializeField] PlayerInventory playerInventory;
    [SerializeField] List<Spawner> spawners = new();
    [SerializeField] private Transform muzzle;
    [SerializeField] AnimEventDispatcher dispatcher;
    [SerializeField] protected string eventKey = string.Empty;

    [Header("Settings")]
    [SerializeField] ItemData ItemBullet;
    [SerializeField] bool useItemBullet = true;
    [SerializeField] GunMode gunMode = GunMode.Normal;

    [SerializeField] float delayTime = 0.5f;
    [SerializeField] GunNormalModeSetting settingNormalMode;
    [SerializeField] GunSpecialModeSetting settingSpecialMode;

    private float currentDelay;
    private GunMode pendingShotMode;
    // Set by TryShooting; the anim event only fires a bullet when this is true
    private bool hasPendingShot;
    private bool warnedMissingRefs;

    private void Awake()
    {
        if (dispatcher != null && !string.IsNullOrEmpty(eventKey))
        {
            dispatcher.GetEvent(eventKey).AddListener(Shoot);
        }
    }

    private void Update()
    {
        if (currentDelay > 0)
        {
            currentDelay -= Time.deltaTime;
            currentDelay = Mathf.Clamp(currentDelay, 0, Mathf.Infinity);
        }
    }

    private void OnEnable()
    {
        SetEnabled(true);
    }

    private void OnDisable()
    {
        SetEnabled(false);
    }
    private void OnDestroy()
    {
        SetEnabled(false);
    }

    public bool CanShoot()
    {
        if (currentDelay > 0) return false;
        if (!HasBulletItem()) return false;
        switch (gunMode)
        {
            case GunMode.Normal:
                return true;
            case GunMode.Special:
                if (skillPoints != null && !skillPoints.TryConsume(settingSpecialMode.skillPointUsage)) return false;
                return true;
        }
        return false;
    }

    // True when the gun does not need items, or the inventory holds at least one bullet item.
    // Missing inventory or item reference counts as "no bullet" so the gun can't fire for free.
    private bool HasBulletItem()
    {
        if (!useItemBullet) return true;

        // The gun prefab can't reference the scene's inventory, so find it if it wasn't assigned
        if (playerInventory == null) playerInventory = FindAnyObjectByType<PlayerInventory>();

        if (playerInventory == null || playerInventory.Inventory == null || ItemBullet == null)
        {
            if (!warnedMissingRefs)
            {
                warnedMissingRefs = true;
                Debug.LogWarning($"GunController: useItemBullet is on but " +
                    $"{(ItemBullet == null ? "ItemBullet" : "PlayerInventory")} is missing, so the gun can't shoot.", this);
            }
            return false;
        }
        return playerInventory.Inventory.CountOf(ItemBullet) > 0;
    }

    public float GetCurrentBullet()
    {
        if (playerInventory == null || ItemBullet == null) return 0;
        return playerInventory.Inventory.CountOf(ItemBullet);
    }

    public bool TryShooting()
    {
        if (!CanShoot()) return false;

        switch (gunMode)
        {
            case GunMode.Normal:
                pendingShotMode = GunMode.Normal;
                foreach (Spawner spawner in spawners)
                {
                    if (spawner == null)
                    {
                        Debug.LogWarning("GunController has a null spawner reference.", this);
                        continue;
                    }
                    spawner.SetPrefab(settingNormalMode.bulletPrefab);
                }
                currentDelay = delayTime;
                break;
            case GunMode.Special:
                pendingShotMode = GunMode.Special;
                foreach (Spawner spawner in spawners)
                {
                    if (spawner == null)
                    {
                        Debug.LogWarning("GunController has a null spawner reference.", this);
                        continue;
                    }
                    spawner.SetPrefab(settingSpecialMode.bulletPrefab);
                }
               
                currentDelay = delayTime;
                break;
        }

        hasPendingShot = true;
        return true;
    }

    private void Shoot()
    {
        // The anim event can fire even when TryShooting failed (e.g. the shoot animation still played),
        // so only spawn bullets for a shot that TryShooting approved.
        if (!hasPendingShot) return;
        hasPendingShot = false;

        // Re-check the item: it may have been used/dropped between TryShooting and the anim event
        if (useItemBullet)
        {
            if (!HasBulletItem()) return;

            playerInventory.Inventory.TryRemove(ItemBullet, 1);
        }

        int amountBullet = 0;
        GunMode shotMode = pendingShotMode;

        // Pay the cost once per shot, not once per spawner
        if (shotMode == GunMode.Special && settingSpecialMode.useThisSetting)
        {
            if (skillPoints != null && !skillPoints.consume(settingSpecialMode.skillPointUsage))
            {
                gunMode = GunMode.Normal;
                return;
            }

            if (useItemBullet)
                playerInventory.Inventory.TryRemove(ItemBullet, 1);
        }

        foreach (Spawner spawner in spawners)
        {
            if (spawner == null)
            {
                Debug.LogWarning("GunController has a null spawner reference.", this);
                continue;
            }
            GameObject bullet = spawner.spawnObject();
            if (bullet == null) continue;
            amountBullet += 1;
            //To do bullet setting
            BaseProjectileMovement baseProjectile = bullet.GetComponent<BaseProjectileMovement>();
            ProjectileDamageApplier damageApplier = bullet.GetComponent<ProjectileDamageApplier>();
            if (damageApplier != null) damageApplier.SetOwner(PlayerController.transform);
            switch (shotMode)
            {
                case GunMode.Normal:
                    skillPoints.SetCurrent(0);

                    if (!settingNormalMode.useThisSetting) break;
                    if (baseProjectile != null) baseProjectile.SetMoveSpeed(settingNormalMode.bulletSpeed);
                    if (damageApplier != null) damageApplier.SetDamge(settingNormalMode.bulletDamage);
                    if (damageApplier != null && settingNormalMode.damageType != null)
                        damageApplier.SetDamageType(settingNormalMode.damageType);

                    if (baseProjectile is HitscanShooter hitscan)
                    {
                        hitscan.SetIgnoreRoot(PlayerController.transform);
                        hitscan.Fire(muzzle.position, muzzle.forward);
                    }
                    break;
                case GunMode.Special:
                    if (!settingSpecialMode.useThisSetting) break;
                    if (baseProjectile != null) baseProjectile.SetMoveSpeed(settingSpecialMode.bulletSpeed);
                    if (damageApplier != null) damageApplier.SetDamge(settingSpecialMode.bulletDamage);
                    if (damageApplier != null && settingSpecialMode.damageType != null)
                        damageApplier.SetDamageType(settingSpecialMode.damageType);

                    if (baseProjectile is HitscanShooter hitscanS)
                    {
                        hitscanS.SetIgnoreRoot(PlayerController.transform);
                        hitscanS.Fire(muzzle.position, muzzle.forward);
                    }
                    break;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (dispatcher != null)
            {
                dispatcher.GetEvent(eventKey).AddListener(Shoot);
            }
        }
        else
        {
            if (dispatcher != null)
            {
                dispatcher.GetEvent(eventKey).RemoveListener(Shoot);
            }
        }
    }

    #region API
    //Gun mode
    public GunMode GetGunMode() => gunMode;
    public void SwitchMode()
    {
        if (gunMode == GunMode.Normal) gunMode = GunMode.Special;
        else gunMode = GunMode.Normal;
    }
    #endregion
}

[Serializable]
public struct GunNormalModeSetting
{
    [Header("Bullet Setting")]
    public GameObject bulletPrefab;
    public bool useThisSetting;
    public float bulletDamage;
    public float bulletSpeed;
    [Tooltip("Leave empty to keep the bullet prefab's own damage type.")]
    public DamageTypeID damageType;
}

[Serializable]
public struct GunSpecialModeSetting
{
    [Header("Mode setting")]
    public float skillPointUsage;

    [Header("Bullet Setting")]
    public GameObject bulletPrefab;
    public bool useThisSetting;
    public float bulletDamage;
    public float bulletSpeed;
    [Tooltip("Leave empty to keep the bullet prefab's own damage type.")]
    public DamageTypeID damageType;
}