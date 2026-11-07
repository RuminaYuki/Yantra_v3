using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class BaseProjectileMovement : MonoBehaviour
{
    public enum HitTargetRepeatMode
    {
        PreventRepeat,
        AllowRepeat
    }

    [Header("Life cycle")]
    [SerializeField] private float _lifeTime = 10f;
    protected float lifetime => _lifeTime;
    private float _lifeTimer;

    [Header("Penetration")]
    [SerializeField, Min(0)] private int maxPenetrations;
    protected int MaxPenetrations
    {
        get => maxPenetrations;
        set => maxPenetrations = value;
    }
    [SerializeField] private bool destroyAfterPenetrationLimit = true;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private HitTargetRepeatMode hitTargetRepeatMode = HitTargetRepeatMode.PreventRepeat;
    [SerializeField, Min(0.01f)] private float repeatDamageInterval = 0.2f;

    [Header("Collision")]
    [Tooltip("Only process projectile hits on these layers.")]
    [SerializeField] private LayerMask _collisionLayer = ~0;
    [Tooltip("Ignore physical collision and hit processing for the owner hierarchy.")]
    [SerializeField] private bool ignoreOwner = true;
    [Tooltip("Skip hit processing for colliders on the owner's layer.")]
    [SerializeField] private bool ignoreOwnerLayer = true;

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 10f;

    private GameObject owner;
    public GameObject Owner => owner;
    private Collider[] projectileColliders;
    private Collider[] ignoredOwnerColliders;

    public event Action<Collider> _hitRegistered;
    protected void HitRegisteredRaise(Collider go)
    {
        _hitRegistered?.Invoke(go);
    }

    protected float moveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = value;
    }

    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private readonly Dictionary<IDamageable, float> lastRepeatHitTimes = new Dictionary<IDamageable, float>();
    private int penetrationsUsed;

    protected virtual void Awake()
    {
        projectileColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        ApplyOwnerCollisionIgnores();
    }

    protected virtual void FixedUpdate()
    {
        _lifeTimer += Time.deltaTime;
        if (_lifeTimer > 0f && _lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessHit(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        ProcessHit(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessHit(other);
    }

    private void ProcessHit(Collider hitCollider)
    {
        if (hitCollider == null ||
            (_collisionLayer.value & (1 << hitCollider.gameObject.layer)) == 0 ||
            IsIgnoredHit(hitCollider))
            return;

        /*if ((obstacleLayer.value & (1 << hitCollider.gameObject.layer)) != 0)
        {
            Destroy(gameObject);
        }*/

        IDamageable damageable = FindDamageable(hitCollider);
        if (damageable == null)
            return;

        bool allowRepeat = hitTargetRepeatMode == HitTargetRepeatMode.AllowRepeat;
        if (!allowRepeat && hitTargets.Contains(damageable))
            return;

        if (allowRepeat &&
            lastRepeatHitTimes.TryGetValue(damageable, out float lastHitTime) &&
            Time.time - lastHitTime < repeatDamageInterval)
            return;

        HitRegisteredRaise(hitCollider);

        if (allowRepeat)
        {
            lastRepeatHitTimes[damageable] = Time.time;
            return;
        }

        if (!allowRepeat)
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

    private bool IsIgnoredHit(Collider hitCollider)
    {
        if (owner == null)
            return false;

        Transform hitTransform = hitCollider.transform;
        if (ignoreOwner &&
            (hitTransform == owner.transform || hitTransform.IsChildOf(owner.transform)))
            return true;

        return ignoreOwnerLayer && hitCollider.gameObject.layer == owner.layer;
    }

    public void NotifyTargetHit()
    {
        Destroy(gameObject, 0.1f);
    }

    public void SetMoveSpeed(float speed) => moveSpeed = speed;

    public void SetOwner(GameObject newOwner)
    {
        ClearOwnerCollisionIgnores();
        owner = newOwner;
        ApplyOwnerCollisionIgnores();

        if (owner != null)
            Debug.Log($"Projectile owner set to {owner.name}");
    }

    private void ApplyOwnerCollisionIgnores()
    {
        if (!ignoreOwner || owner == null)
            return;

        if (projectileColliders == null)
            projectileColliders = GetComponentsInChildren<Collider>(true);

        ignoredOwnerColliders = owner.GetComponentsInChildren<Collider>(true);
        foreach (Collider projectileCollider in projectileColliders)
        {
            if (projectileCollider == null ||
                !projectileCollider.enabled ||
                !projectileCollider.gameObject.activeInHierarchy)
                continue;

            foreach (Collider ownerCollider in ignoredOwnerColliders)
            {
                if (ownerCollider == null ||
                    !ownerCollider.enabled ||
                    !ownerCollider.gameObject.activeInHierarchy ||
                    ownerCollider == projectileCollider)
                    continue;

                Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }
    }

    private void ClearOwnerCollisionIgnores()
    {
        if (projectileColliders == null || ignoredOwnerColliders == null)
            return;

        foreach (Collider projectileCollider in projectileColliders)
        {
            if (projectileCollider == null ||
                !projectileCollider.enabled ||
                !projectileCollider.gameObject.activeInHierarchy)
                continue;

            foreach (Collider ownerCollider in ignoredOwnerColliders)
            {
                if (ownerCollider == null || ownerCollider == projectileCollider)
                    continue;

                Physics.IgnoreCollision(projectileCollider, ownerCollider, false);
            }
        }

        ignoredOwnerColliders = null;
    }
}
