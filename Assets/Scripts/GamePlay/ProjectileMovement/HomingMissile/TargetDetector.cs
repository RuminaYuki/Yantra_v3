using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TargetDetector : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float detectionRange = 20f;
    [SerializeField, Range(0f, 360f)] private float detectionAngle = 60f;
    [SerializeField] private float scanInterval = 0.1f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask obstructionLayer = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showRuntimeRay = true;
    [SerializeField] private Color detectionColor = Color.yellow;
    [SerializeField] private Color targetColor = Color.green;
    [SerializeField] private Color blockedColor = Color.red;

    private List<Func<Transform, Transform>> _modifiers = new();
    private readonly HashSet<Transform> ignoredTargets = new();
    private float scanTimer;
    private Transform cachedTarget;

    public Transform FindTarget(Transform origin)
    {
        if (origin == null)
            return null;

        if (cachedTarget != null &&
            cachedTarget.gameObject.activeInHierarchy &&
            IsInDetectionCone(origin, cachedTarget))
            return cachedTarget;

        cachedTarget = null;

        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f)
            return null;

        scanTimer = Mathf.Max(0.01f, scanInterval);
        Collider[] candidates = Physics.OverlapSphere(
            origin.position,
            detectionRange,
            enemyLayer,
            QueryTriggerInteraction.Ignore);

        Transform nearestTarget = null;
        float bestDistanceSqr = float.PositiveInfinity;

        foreach (Collider candidate in candidates)
        {
            IDamageable damageable = FindDamageable(candidate);
            if (damageable == null)
                continue;

            Transform candidateTarget = ((Component)damageable).transform;
            if (ignoredTargets.Contains(candidateTarget))
                continue;

            Vector3 direction = candidateTarget.position - origin.position;
            float distanceSqr = direction.sqrMagnitude;
            float angle = Vector3.Angle(origin.forward, direction);

            /*if (distanceSqr <= 0.001f || angle > detectionAngle * 0.5f)
                continue;*/

            if (requireLineOfSight &&
                Physics.Raycast(origin.position, direction.normalized, out RaycastHit hit,
                    Mathf.Sqrt(distanceSqr), obstructionLayer, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(candidateTarget) &&
                hit.transform != candidateTarget)
                continue;

            if (distanceSqr < bestDistanceSqr)
            {
                nearestTarget = candidateTarget;
                bestDistanceSqr = distanceSqr;
            }
        }

        cachedTarget = nearestTarget;

        foreach (var modifier in _modifiers)
        {
            cachedTarget = modifier(cachedTarget);
        }

        DrawRuntimeDetection(origin, cachedTarget);
        return cachedTarget;
    }

    private IDamageable FindDamageable(Collider candidate)
    {
        MonoBehaviour[] behaviours = candidate.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            IDamageable damageable = behaviour as IDamageable;
            if (damageable != null)
                return damageable;
        }

        return null;
    }

    private bool IsInDetectionCone(Transform origin, Transform candidate)
    {
        Vector3 direction = candidate.position - origin.position;
        return direction.sqrMagnitude > 0.001f &&
               direction.sqrMagnitude <= detectionRange * detectionRange &&
               Vector3.Angle(origin.forward, direction) <= detectionAngle * 0.5f;
    }

    public void ClearTarget()
    {
        cachedTarget = null;
        scanTimer = 0f;
    }

    public void IgnoreTarget(Transform target)
    {
        if (target == null)
            return;

        ignoredTargets.Add(target);
        if (cachedTarget == target ||
            (cachedTarget != null &&
             (cachedTarget.IsChildOf(target) || target.IsChildOf(cachedTarget))))
            ClearTarget();
    }

    public void ClearIgnoredTargets()
    {
        ignoredTargets.Clear();
        ClearTarget();
    }

    private void DrawRuntimeDetection(Transform origin, Transform target)
    {
        if (!showRuntimeRay)
            return;

        Debug.DrawRay(origin.position, origin.forward * detectionRange, detectionColor, scanInterval);
        if (target != null)
            Debug.DrawLine(origin.position, target.position, targetColor, scanInterval);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos)
            return;

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        float halfAngle = detectionAngle * 0.5f;

        Gizmos.color = detectionColor;
        Gizmos.DrawWireSphere(origin, detectionRange);
        Gizmos.DrawLine(origin, origin + forward * detectionRange);

        DrawConeEdge(origin, forward, halfAngle, detectionRange);
        DrawConeEdge(origin, forward, -halfAngle, detectionRange);
        //DrawConeEdge(origin, Quaternion.AngleAxis(90f, transform.up) * forward, halfAngle, detectionRange);
        //DrawConeEdge(origin, Quaternion.AngleAxis(90f, transform.up) * forward, -halfAngle, detectionRange);

        if (cachedTarget == null)
            return;

        Gizmos.color = targetColor;
        Gizmos.DrawLine(origin, cachedTarget.position);
        Gizmos.DrawWireSphere(cachedTarget.position, 0.25f);
    }

    private void DrawConeEdge(Vector3 origin, Vector3 direction, float angle, float length)
    {
        Vector3 edgeDirection = Quaternion.AngleAxis(angle, transform.up) * direction;
        Gizmos.DrawLine(origin, origin + edgeDirection.normalized * length);
    }

    public void AddModifier(Func<Transform, Transform> modifier)
    {
        _modifiers.Add(modifier);
    }
}
