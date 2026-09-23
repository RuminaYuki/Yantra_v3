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

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 10f;

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

    }

    protected virtual void Update()
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
        if ((obstacleLayer.value & (1 << hitCollider.gameObject.layer)) != 0)
        {
            Destroy(gameObject);
        }

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

    public void NotifyTargetHit()
    {
        Destroy(gameObject, 0.1f);
    }

    public void SetMoveSpeed(float speed) => moveSpeed = speed;
}
