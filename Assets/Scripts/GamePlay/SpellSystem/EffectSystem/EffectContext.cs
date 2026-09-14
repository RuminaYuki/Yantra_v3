using UnityEngine;

public readonly struct EffectContext
{
    public readonly GameObject Owner;
    public readonly Vector3 Origin;
    public readonly Vector3 Direction;
    public readonly EffectDefinition Definition;

    public EffectContext(
        GameObject owner,
        Vector3 origin,
        Vector3 direction,
        EffectDefinition definition)
    {
        Owner = owner;
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

        GameObject projectile = Object.Instantiate(
            prefab,
            context.Origin,
            Quaternion.LookRotation(context.Direction));

        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.velocity = context.Direction * context.Definition.ProjectileSpeed;
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

        Health health = context.Owner.GetComponentInParent<Health>();
        if (health != null)
        {
            health.Heal(context.Definition.Power);
        }
    }
}
