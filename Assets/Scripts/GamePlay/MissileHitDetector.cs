using System.Collections.Generic;
using UnityEngine;

public class MissileHitDetector : MonoBehaviour
{
    [Header("Penetration")]
    [SerializeField, Min(0)] private int maxPenetrations;
    [SerializeField] private bool destroyAfterPenetrationLimit = true;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private MissileDamageApplier damageApplier;
    [SerializeField] private HomingMissile missile;

    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private int penetrationsUsed;

    private void Awake()
    {
        if (damageApplier == null)
            damageApplier = GetComponent<MissileDamageApplier>();
        if (missile == null)
            missile = GetComponent<HomingMissile>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessHit(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other);
    }

    private void ProcessHit(Collider hitCollider)
    {
        if ((obstacleLayer.value & (1 << hitCollider.gameObject.layer)) != 0)
        {
            Destroy(gameObject);
        }

        if (damageApplier == null)
            return;

        IDamageable damageable = FindDamageable(hitCollider);
        if (damageable == null || hitTargets.Contains(damageable))
            return;

        if (!damageApplier.ApplyDamage(hitCollider))
            return;

        hitTargets.Add(damageable);
        penetrationsUsed++;

        if (destroyAfterPenetrationLimit && penetrationsUsed > maxPenetrations)
        {
            if (missile != null)
                missile.NotifyTargetHit();
            else
                Destroy(gameObject);
        }
    }

    private IDamageable FindDamageable(Collider hitCollider)
    {
        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            IDamageable damageable = behaviour as IDamageable;
            if (damageable != null)
                return damageable;
        }

        return null;
    }
}
