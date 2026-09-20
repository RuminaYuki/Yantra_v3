using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class BaseProjectileMovement : MonoBehaviour
{
    [Header("Life cycle")]
    [SerializeField] private float _lifeTime = 10f;
    protected float lifetime => _lifeTime;
    private float _lifeTimer;
    protected float lifeTimer 
    {
        get => _lifeTimer; set => _lifeTimer = value;
    }

    [Header("Penetration")]
    [SerializeField, Min(0)] private int maxPenetrations;
    [SerializeField] private bool destroyAfterPenetrationLimit = true;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private ProjectileDamageApplier damageApplier;

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 10f;
    protected float moveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = value;
    }

    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private int penetrationsUsed;

    protected virtual void Awake()
    {
        if (damageApplier == null)
            damageApplier = GetComponent<ProjectileDamageApplier>();
    }

    protected virtual void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifetime > 0f && lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }
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
            NotifyTargetHit();
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

    public void NotifyTargetHit()
    {
        Destroy(gameObject);
    }

    public void SetMoveSpeed(float speed) => moveSpeed = speed;
}
