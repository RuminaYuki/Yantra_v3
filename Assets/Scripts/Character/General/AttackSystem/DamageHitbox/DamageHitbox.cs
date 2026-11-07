using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger hitbox: deals damage to any IDamageable that enters it.
/// Lives on the prefab itself, so every instance (e.g. thorns spawned by SplineInstantiate)
/// carries its own hitbox. Damage can be overridden at runtime via SetDamage / SetDamageType.
/// The trigger collider can live on another GameObject (e.g. a child) - a DamageHitboxRelay
/// is added to it at runtime to forward its trigger events here.
/// </summary>
public class DamageHitbox : MonoBehaviour
{
    [Header("Collider")]
    [Tooltip("Trigger collider used as the hitbox. Leave empty to use the collider on this GameObject.")]
    [SerializeField] private Collider hitCollider;

    [Header("Damage")]
    [SerializeField] private float damageAmount = 10f;
    [Tooltip("Not required - leave empty if this hitbox doesn't need a specific damage type.")]
    [SerializeField] private DamageTypeID damageType;

    [Header("Filter")]
    [Tooltip("Only these layers take damage (e.g. Player) - prevents hitting the owner.")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Header("Behaviour")]
    [Tooltip("Damage each target only once until this hitbox is disabled/enabled again.")]
    [SerializeField] private bool hitOncePerTarget = true;

    // A target often has several colliders; remember who was hit so it isn't damaged once per collider.
    private readonly HashSet<IDamageable> _hitTargets = new();

    /// <summary>Raised after this hitbox damages a target.</summary>
    public event Action OnHit;

    private bool UsesOwnCollider => hitCollider != null && hitCollider.gameObject == gameObject;

    private void Reset()
    {
        // Auto-pick and set isTrigger when the component is added.
        hitCollider = GetComponent<Collider>();
        if (hitCollider != null)
            hitCollider.isTrigger = true;
    }

    private void Awake()
    {
        if (hitCollider == null)
            hitCollider = GetComponent<Collider>();

        if (hitCollider == null)
        {
            Debug.LogWarning("[DamageHitbox] No hit collider assigned or found on this GameObject.", this);
            return;
        }

        // Unity only sends OnTriggerEnter to the collider's own GameObject (or its Rigidbody's),
        // so a collider elsewhere needs a relay to forward the event back to this hitbox.
        // Reuse a relay placed there in the editor instead of stacking a second one.
        if (!UsesOwnCollider)
        {
            if (!hitCollider.TryGetComponent(out DamageHitboxRelay relay))
                relay = hitCollider.gameObject.AddComponent<DamageHitboxRelay>();
            relay.Init(this);
        }
    }

    private void OnEnable()
    {
        _hitTargets.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        // A collider elsewhere is handled by its relay; ignore anything else hitting this GameObject.
        if (!UsesOwnCollider) return;

        HandleHit(other);
    }

    public void HandleHit(Collider other)
    {
        // Trigger messages still reach disabled components, so check explicitly.
        if (!isActiveAndEnabled)
            return;

        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        IDamageable damageable = FindDamageable(other);
        if (damageable == null)
            return;

        if (hitOncePerTarget && !_hitTargets.Add(damageable))
            return;

        damageable.TakeDamage(damageAmount);
        OnHit?.Invoke();
        if (damageType != null)
            damageable.DamageType(damageType);
    }

    private static IDamageable FindDamageable(Collider hitCollider)
    {
        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IDamageable damageable)
                return damageable;
        }

        return null;
    }

    public void SetDamage(float amount)
    {
        damageAmount = amount;
    }

    public void SetDamageType(DamageTypeID type)
    {
        damageType = type;
    }
}
