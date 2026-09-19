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

    [SerializeField] SkillPoints skillPoints;
    [SerializeField] List<Spawner> spawners = new();

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
    [SerializeField] Animator animator;
    [SerializeField] GunNormalModeSetting settingNormalMode;
    [SerializeField] GunSpecialModeSetting settingSpecialMode;



    private float currentDelay;

    private void Awake()
    {
        CurentAmmo = maxAmmo;
    }

    private void Update()
    {
        if (currentDelay > 0)
        {
            currentDelay -= Time.deltaTime;
            currentDelay = Mathf.Clamp(currentDelay, 0, Mathf.Infinity);
        }
    }

    private void OnDestroy()
    {
        SetEnabled(false);
    }

    public bool TryShooting()
    {
        if (currentDelay > 0) return false;

        if (animator == null) return false;

        if (curentAmmo == 0) return false;

        switch (gunMode)
        {
            case GunMode.Normal:
                foreach (Spawner spawner in spawners)
                {
                    spawner.SetPrefab(settingNormalMode.bulletPrefab);
                }
                if (string.IsNullOrEmpty(settingNormalMode.stateName))
                {
                    Shoot();
                    break;
                }
                animator.CrossFade(settingNormalMode.stateName, settingNormalMode.crossFade, settingNormalMode.layerIndex);    
                break;
            case GunMode.Special:
                if (!skillPoints.consume(settingSpecialMode.skillPointUsage)) return false;
                foreach (Spawner spawner in spawners)
                {
                    spawner.SetPrefab(settingSpecialMode.bulletPrefab);
                }
                if (!string.IsNullOrEmpty(settingSpecialMode.stateName))
                {
                    Shoot();
                    break;
                }
                animator.CrossFade(settingSpecialMode.stateName, settingNormalMode.crossFade, settingNormalMode.layerIndex);
                break;
        }
        currentDelay = delayTime;
        return true;
    }

    private void Shoot()
    {
        foreach (Spawner spawner in spawners)
        {
            GameObject bullet = spawner.spawnObject();
            if (bullet == null) continue;
            //To do bullet setting
            BaseProjectileMovement baseProjectile = bullet.GetComponent<BaseProjectileMovement>();
            ProjectileDamageApplier damageApplier = bullet.GetComponent<ProjectileDamageApplier>();
            switch (gunMode)
            {
                case GunMode.Normal:
                    if (!settingNormalMode.useThisSetting) break;
                    if (baseProjectile != null) baseProjectile.SetMoveSpeed(settingNormalMode.bulletSpeed);
                    if (damageApplier != null) damageApplier.SetDamge(settingNormalMode.bulletDamage);
                    break;
                case GunMode.Special:
                    if (!settingSpecialMode.useThisSetting) break;
                    if (baseProjectile != null) baseProjectile.SetMoveSpeed(settingSpecialMode.bulletSpeed);
                    if (damageApplier != null) damageApplier.SetDamge(settingSpecialMode.bulletDamage);
                    break;
            }
            CurentAmmo -= 1;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (handleSpawnEvent != null)
            {
                handleSpawnEvent.Raised += Shoot;
            }
        }
        else
        {
            if (handleSpawnEvent != null)
            {
                handleSpawnEvent.Raised -= Shoot;
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
    public int SetMaxAmmo(int value) => maxAmmo = value;

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

    [Header("Animator")]
    public string stateName;
    public int layerIndex;
    public float crossFade;
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

    [Header("Animator")]
    public string stateName;
    public int layerIndex;
    public float crossFade;
}