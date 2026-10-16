using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct RingSampleParameters
{
    public float radius;
    public float minDistance;
    public int directionCount;
}

public static class RingPointSampler
{
    private static readonly List<Vector3> s_validPoints = new();

    // Casts out in evenly spaced directions, picks a random distance in each open lane,
    // then returns one of the points the validator accepts.
    public static bool TryGetPoint(StandablePointValidator validator, Vector3 origin,
        in RingSampleParameters parameters, out Vector3 point)
    {
        float radius = Mathf.Max(0.01f, parameters.radius);
        float minDistance = Mathf.Clamp(parameters.minDistance, 0.01f, radius);
        int directionCount = Mathf.Max(1, parameters.directionCount);
        float bodyRadius = validator.BodyRadius;

        s_validPoints.Clear();

        for (int i = 0; i < directionCount; i++)
        {
            float angle = 360f / directionCount * i;

            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float availableDistance = radius;

            if (Physics.SphereCast(origin, bodyRadius, direction,
                    out RaycastHit hit, radius, validator.ObstacleLayer))
            {
                availableDistance = hit.distance;
            }

            availableDistance -= bodyRadius;

            if (availableDistance < minDistance)
                continue;

            float distance = Random.Range(minDistance, availableDistance);
            Vector3 candidate = origin + direction * distance;

            if (!validator.TryValidatePoint(origin, candidate, out Vector3 groundPoint))
                continue;

            s_validPoints.Add(groundPoint);
        }

        if (s_validPoints.Count == 0)
        {
            point = default;
            return false;
        }

        point = s_validPoints[Random.Range(0, s_validPoints.Count)];

        return true;
    }
}
