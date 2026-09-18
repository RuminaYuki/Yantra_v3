using UnityEngine;

public class ProjectileShooter : MonoBehaviour
{
    [SerializeField] protected Transform _firePoint;
    [SerializeField] protected ProjectileBullet _projectilePrefab;

    public virtual void Shoot()
    {
        if (_firePoint == null || _projectilePrefab == null)
            return;

        ProjectileBullet projectile = Instantiate(_projectilePrefab, _firePoint.position, _firePoint.rotation);
        projectile.Launch();
    }

    public virtual void ShootAtTarget(Vector3 targetPosition)
    {
        if (_firePoint == null || _projectilePrefab == null)
            return;

        ProjectileBullet projectile = Instantiate(_projectilePrefab, _firePoint.position, _firePoint.rotation);
        projectile.LaunchAtTarget(targetPosition);
    }
}
