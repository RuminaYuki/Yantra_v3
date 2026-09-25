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
    [SerializeField] List<Spawner> spawners = new();
    [SerializeField] private Transform muzzle;
    [SerializeField] AnimEventDispatcher dispatcher;
    [SerializeField] protected string eventKey = string.Empty;

    [Header("Settings")]
    [SerializeField] GunMode gunMode = GunMode.Normal;
    [SerializeField] int curentAmmo = 0;
    private int CurentAmmo
    {
        get => curentAmmo;
        set
        {
            curentAmmo = Mathf.Clamp(value, 0, maxAmmo);
        }
    }

    [SerializeField] int maxAmmo = 2;
    [SerializeField] float delayTime = 0.5f;
    [SerializeField] VoidEventChannelSO handleSpawnEvent;
    [SerializeField] GunNormalModeSetting settingNormalMode;
    [SerializeField] GunSpecialModeSetting settingSpecialMode;

    private float currentDelay;
    private bool isSpawnEventSubscribed;
    private GunMode pendingShotMode;
    private bool hasPendingShot;

    private void Awake()
    {
        CurentAmmo = maxAmmo;

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

    public bool TryShooting()
    {
        if (currentDelay > 0) return false;

        if (curentAmmo == 0) return false;

        switch (gunMode)
        {
            case GunMode.Normal:
                pendingShotMode = GunMode.Normal;
                hasPendingShot = true;
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
                if (skillPoints != null && !skillPoints.TryConsume(settingSpecialMode.skillPointUsage)) return false;
                pendingShotMode = GunMode.Special;
                hasPendingShot = true;
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
        GunMode shotMode = hasPendingShot ? pendingShotMode : gunMode;
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

                    if (baseProjectile is HitscanShooter hitscan)
                    {
                        hitscan.Fire(muzzle.position, muzzle.forward);
                    }
                    break;
                case GunMode.Special:
                    if (!settingSpecialMode.useThisSetting) break;
                    if (skillPoints != null && !skillPoints.consume(settingSpecialMode.skillPointUsage)) 
                    {
                        gunMode = GunMode.Normal;
                        return;
                    }
                    if (baseProjectile != null) baseProjectile.SetMoveSpeed(settingSpecialMode.bulletSpeed);
                    if (damageApplier != null) damageApplier.SetDamge(settingSpecialMode.bulletDamage);

                    if (baseProjectile is HitscanShooter hitscanS)
                    {
                        hitscanS.Fire(muzzle.position, muzzle.forward);
                    }
                    break;
            }
        }
        if (amountBullet != 0) CurentAmmo -= 1;
        hasPendingShot = false;
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
    //Current Ammo API
    public int GetCurentAmmo(int value) => CurentAmmo;
        //For demo only
    public void Reload() => CurentAmmo = maxAmmo;

    //Max Ammo API
    public int GetMaxAmmo() => maxAmmo;
    //For upgrade max-ammo
    public int SetMaxAmmo(int value)
    {
        if (value <= 0)
        {
            Debug.LogWarning("Max ammo must be greater than zero.");
            return maxAmmo;
        }

        maxAmmo = value;
        CurentAmmo = Mathf.Min(CurentAmmo, maxAmmo);
        return maxAmmo;
    }

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

}