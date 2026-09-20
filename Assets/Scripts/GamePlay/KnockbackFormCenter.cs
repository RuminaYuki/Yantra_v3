using System;
using System.Collections.Generic;
using UnityEngine;

public class KnockbackFormCenter : MonoBehaviour
{
    [SerializeField] KnockbackParameters parameters;
    [SerializeField] bool playOnAwaken = false;

    private KnockbackParameters currentParameters = new();

    private void Awake()
    {
        if (playOnAwaken)
        {
            TryToExecuteKnockback(parameters);
        }
    }

    public bool TryToExecuteKnockback(KnockbackParameters parameters)
    {
        currentParameters = parameters;
        if (currentParameters.Radius <= 0f || currentParameters.Power <= 0f)
        {
            return false;
        }

        Destroy(gameObject, currentParameters.Duration == 0 ? 0.1f : currentParameters.Duration);

        float angle = currentParameters.Angle <= 0f ? 360f : currentParameters.Angle;
        int targetLayer = currentParameters.TargetLayer.value == 0
            ? Physics.AllLayers
            : currentParameters.TargetLayer.value;
        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            currentParameters.Radius,
            targetLayer,
            QueryTriggerInteraction.Ignore);
        HashSet<Rigidbody> affectedBodies = new HashSet<Rigidbody>();
        bool didApplyKnockback = false;

        foreach (Collider collider in colliders)
        {
            if (currentParameters.Owner != null &&
                (collider.gameObject == currentParameters.Owner ||
                 collider.transform.IsChildOf(currentParameters.Owner.transform)))
            {
                continue;
            }

            Vector3 direction = collider.transform.position - transform.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon ||
                Vector3.Angle(transform.forward, direction) > angle * 0.5f)
            {
                continue;
            }

            direction.Normalize();
            if (currentParameters.Reverse)
            {
                direction = -direction;
            }

            IKnockbackReceiver receiver = collider.GetComponentInParent<IKnockbackReceiver>();
            if (receiver != null)
            {
                //Do something to enemy else Flag Knockback
                receiver.ApplyKnockback(direction, parameters);
                didApplyKnockback = true;
                continue;
            }

            Rigidbody body = collider.attachedRigidbody;
            if (body != null && affectedBodies.Add(body))
            {
                body.AddForce(direction * currentParameters.Power, currentParameters.ForceMode);
                didApplyKnockback = true;
            }
        }

        return didApplyKnockback;
    }

    private void OnValidate()
    {
        if (currentParameters.Radius != parameters.Radius)
        {
            currentParameters.Radius = parameters.Radius;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, currentParameters.Radius);
    }
}

[Serializable]
public struct KnockbackParameters
{
    public float Power;
    public float Radius;
    [Range(0f, 360f)] public float Angle;
    public float Duration;
    public bool Reverse;
    public GameObject Owner;
    public LayerMask TargetLayer;
    public ForceMode ForceMode;

    public KnockbackParameters(
        float power,
        float radius,
        float angle,
        float duration,
        bool reverse,
        GameObject owner,
        LayerMask targetLayer,
        ForceMode forceMode = ForceMode.Impulse)
    {
        Power = power;
        Radius = radius;
        Angle = Mathf.Clamp(angle, 0f, 360f);
        Duration = duration;
        Reverse = reverse;
        Owner = owner;
        TargetLayer = targetLayer;
        ForceMode = forceMode;
    }

    public KnockbackParameters(float power, float radius,float duration, GameObject owner)
        : this(power, radius, 360f, duration, false, owner, Physics.AllLayers)
    {
    }
}