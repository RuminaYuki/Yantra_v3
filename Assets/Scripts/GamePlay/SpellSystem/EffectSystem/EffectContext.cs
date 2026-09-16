using System.Collections.Generic;
using UnityEngine;

public readonly struct EffectContext
{
    public readonly GameObject Owner;
    public readonly GameObject EffectOwner;
    public readonly Vector3 Origin;
    public readonly Vector3 Direction;
    public readonly EffectDefinition Definition;

    public EffectContext(
        GameObject owner,
        GameObject effectOwner,
        Vector3 origin,
        Vector3 direction,
        EffectDefinition definition)
    {
        Owner = owner;
        EffectOwner = effectOwner;
        Origin = origin;
        Direction = direction.sqrMagnitude > 0f
            ? direction.normalized
            : Vector3.forward;
        Definition = definition;
    }
}

public interface IEffectExecutor
{
    void Execute(EffectContext context);
}

public sealed class HomingMissileEffect : IEffectExecutor
{
    public void Execute(EffectContext context)
    {
        GameObject prefab = context.Definition.ProjectilePrefab;
        if (prefab == null)
        {
            Debug.LogWarning(
                "Homing Missile requires a projectile prefab.",
                context.Owner);
            return;
        }

        if (!context.Definition.Animator)
        {
            Animator animator = context.Definition.Animator.Value;
            if (animator == null) return;

            VoidEventChannelSO eventChannel = animator.gameObject.GetComponent<HandleShoting>().GetEventChannel();
            BulletSpawner[] spawners = context.EffectOwner.gameObject.GetComponentsInChildren<BulletSpawner>();
            foreach (BulletSpawner spawn in spawners)
            {
                spawn.SetEventChannel(eventChannel);
            }

            if (!string.IsNullOrEmpty(context.Definition.AnimationName))
            {
                animator.CrossFade(context.Definition.AnimationName, 0.2f, context.Definition.LayerIndex);
            }
        }
    }
}

public sealed class RadialPushEffect : IEffectExecutor
{
    public void Execute(EffectContext context)
    {
        Collider[] colliders = Physics.OverlapSphere(
            context.Origin,
            context.Definition.Radius);

        foreach (Collider collider in colliders)
        {
            if (collider.gameObject == context.Owner)
            {
                continue;
            }

            Rigidbody body = collider.attachedRigidbody;
            if (body != null)
            {
                body.AddExplosionForce(
                    context.Definition.Power,
                    context.Origin,
                    context.Definition.Radius);
            }
        }
    }
}

public sealed class SelfHealEffect : IEffectExecutor
{
    public void Execute(EffectContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        Health health = context.Owner.GetComponentInChildren<Health>();
        if (health != null)
        {
            health.Heal(context.Definition.Power);
        }
    }
}
