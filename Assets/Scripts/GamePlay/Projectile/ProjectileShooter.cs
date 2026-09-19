using UnityEngine;

public class ProjectileShooter : MonoBehaviour
{
    [SerializeField] private Transform _firePoint;
    [SerializeField] private ProjectileBullet _projectilePrefab;
    [SerializeField] private string _poolTag;
    [Tooltip("Bullet owner, so it won't hit its own shooter. Defaults to the shooter's root transform if left unset (ProjectileShooter is often on a weapon/mount that's a child of the actual character).")]
    [SerializeField] private Transform _owner;

    [Header("Bullet Override")]
    [Tooltip("Overrides the bullet prefab's own defaults, so multiple weapons can share one prefab with different damage/speed/trajectory.")]
    [SerializeField]
    private ProjectileBullet.ProjectileConfig _bulletConfig = new ProjectileBullet.ProjectileConfig
    {
        aimMode = ProjectileBullet.AimMode.BySpeed,
        apexHeight = 2f,
        projectileSpeed = 15f,
        gravity = 9.81f,
        lifetime = 5f,
        damage = 10f
    };

    // Cached by SetTarget()/SetBulletConfig() ahead of time, consumed by the next parameterless Execute() call.
    private Vector3? _pendingTarget;

    public void Shoot()
    {
        if (_firePoint == null) return;

        ProjectileBullet projectile = SpawnProjectile(_firePoint.position, _firePoint.rotation);
        if (projectile != null) projectile.Launch();
    }

    public void ShootAtTarget(Vector3 targetPosition)
    {
        if (_firePoint == null) return;

        ProjectileBullet projectile = SpawnProjectile(_firePoint.position, _firePoint.rotation);
        if (projectile != null) projectile.LaunchAtTarget(targetPosition);
    }

    // Arms an aimed shot ahead of time (e.g. on state enter) so an Animation Event can fire it later, at the right frame.
    public void SetTarget(Vector3 targetPosition)
    {
        _pendingTarget = targetPosition;
    }

    public void ClearTarget()
    {
        _pendingTarget = null;
    }

    public void SetBulletConfig(ProjectileBullet.ProjectileConfig config)
    {
        _bulletConfig = config;
    }

    // Switches which bullet this shooter fires (e.g. multiple bullet types sharing one shooter).
    // Pass fallbackPrefab too when the pool might not be ready yet, so the two stay in sync.
    public void SetBulletType(string poolTag, ProjectileBullet fallbackPrefab = null)
    {
        _poolTag = poolTag;
        if (fallbackPrefab != null)
            _projectilePrefab = fallbackPrefab;
    }

    // Parameterless entry point for Animation Events (and for wiring into a UnityEvent/Action) — fires using
    // whatever target/config was last armed via SetTarget()/SetBulletConfig(), falling back to a straight shot.
    public void Execute()
    {
        if (_pendingTarget.HasValue)
            ShootAtTarget(_pendingTarget.Value);
        else
            Shoot();
    }

    private ProjectileBullet SpawnProjectile(Vector3 position, Quaternion rotation)
    {
        bool poolReady = ObjectPooler.Instance != null && !string.IsNullOrEmpty(_poolTag);
        ProjectileBullet projectile = poolReady ? SpawnFromPool(position, rotation) : SpawnDirect(position, rotation);

        if (projectile != null)
        {
            projectile.SetOwner(_owner != null ? _owner : transform.root);
            projectile.Configure(_bulletConfig);
        }

        return projectile;
    }

    private ProjectileBullet SpawnFromPool(Vector3 position, Quaternion rotation)
    {
        ProjectileBullet result = null;

        ObjectPooler.Instance.SpawnFromPool(_poolTag, position, rotation, obj =>
        {
            if (obj.TryGetComponent(out ProjectileBullet p))
            {
                p.myPoolTag = _poolTag;
                result = p;
            }
        });

        return result;
    }

    // Fallback path used when no pool is set up yet (missing tag, or ObjectPooler not ready).
    private ProjectileBullet SpawnDirect(Vector3 position, Quaternion rotation)
    {
        if (_projectilePrefab == null)
        {
            Debug.LogWarning("[ProjectileShooter] No pool tag and no prefab — can't shoot.");
            return null;
        }

        return Instantiate(_projectilePrefab, position, rotation);
    }
}
