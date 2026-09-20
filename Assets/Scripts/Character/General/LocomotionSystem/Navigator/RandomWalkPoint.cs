using System.Collections.Generic;
using UnityEngine;

public class RandomWalkPoint : MonoBehaviour
{
    [SerializeField] private float _radius = 5f;
    [SerializeField] private float _bodyRadius = 0.4f;
    [SerializeField] private float _minDistance = 1.5f;
    [SerializeField] private int _directionCount = 16;
    [SerializeField] private LayerMask _obstacleLayer;
    [SerializeField] private LayerMask _groundLayer;

    public bool TryGetRandomPoint(out Vector3 point)
    {
        return TryGetRandomPoint(transform.position, out point);
    }

    public bool TryGetRandomPoint(Vector3 origin, out Vector3 point)
    {
        return TryGetRandomPoint(origin,_radius, _minDistance, out point);
    }
    public bool TryGetRandomPoint(Vector3 origin, float radius, float minDistance, out Vector3 point)
    {
        return TryGetRandomPoint(origin,radius,_bodyRadius,
            minDistance,_directionCount,_obstacleLayer,_groundLayer, out point);
    }
    public bool TryGetRandomPoint(Vector3 origin, float radius,
        float bodyRadius, float minDistance, int directionCount,
        LayerMask obstacleLayer, LayerMask groundLayer, out Vector3 point)
    {
        radius = Mathf.Max(0.01f, radius);
        bodyRadius = Mathf.Max(0.01f, bodyRadius);
        minDistance = Mathf.Clamp(minDistance, 0.01f, radius);
        directionCount = Mathf.Max(1, directionCount);

        List<Vector3> validPoints = new();

        for (int i = 0; i < directionCount; i++)
        {
            float angle = 360f / directionCount * i;

            Vector3 direction =  Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float availableDistance = radius;

            if (Physics.SphereCast(origin, bodyRadius, direction,
                    out RaycastHit hit, radius, obstacleLayer))
            {
                availableDistance = hit.distance;
            }

            availableDistance -= bodyRadius;

            if (availableDistance < minDistance)
                continue;

            float distance = Random.Range(minDistance, availableDistance);

            Vector3 candidate = origin + direction * distance;
            Vector3 pathStart = origin + Vector3.up * bodyRadius;
            Vector3 pathEnd = candidate + Vector3.up * bodyRadius;

            // Prevent random points from crossing an obstacle.
            if (Physics.Linecast(
                    pathStart,
                    pathEnd,
                    obstacleLayer,
                    QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (!TryGetGround(
                    candidate,
                    groundLayer,
                    out Vector3 groundPoint))
            {
                continue;
            }

            // Prevent the destination from overlapping an obstacle.
            Vector3 overlapCenter =
                groundPoint + Vector3.up * bodyRadius;

            if (Physics.CheckSphere(
                    overlapCenter,
                    bodyRadius,
                    obstacleLayer,
                    QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            validPoints.Add(groundPoint);
        }

        if (validPoints.Count == 0)
        {
            point = default;
            return false;
        }

        point = validPoints[Random.Range(0, validPoints.Count)];

        return true;
    }

    private bool TryGetGround(Vector3 position,
        LayerMask groundLayer, out Vector3 groundPoint)
    {
        Vector3 origin = position + Vector3.up * 2f;

        if (Physics.Raycast(origin,Vector3.down,
                out RaycastHit hit, 4f, groundLayer))
        {
            groundPoint = hit.point;
            return true;
        }

        groundPoint = default;
        return false;
    }
}
