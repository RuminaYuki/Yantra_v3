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
        if (useItemBullet && playerInventory != null && playerInventory.Inventory.CountOf(ItemBullet) <= 0) return false;
        switch (gunMode)
        {
            case GunMode.Normal:
                return true;
            case GunMode.Special:
                if (skillPoints != null && !skillPoints.TryConsume(settingSpecialMode.skillPointUsage)) return false;                return true;
        }
        return false;
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

        return true;
    }

    private void Shoot()
    {
        int amountBullet = 0;
        GunMode shotMode = pendingShotMode;

        // Pay the cost once per shot, not once per spawner
        if (shotMode == GunMode.Special && settingSpecialMode.useThisSetting)
        {
            // Check the item first so skill points are not lost if the bullet item is gone
            bool needItem = useItemBullet && playerInventory != null;
            if (needItem && playerInventory.Inventory.CountOf(ItemBullet) <= 0)
                return;

            if (skillPoints != null && !skillPoints.consume(settingSpecialMode.skillPointUsage))
            {
                gunMode = GunMode.Normal;
                return;
            }

            if (needItem)
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