using System;
using System.Collections.Generic;
using UnityEngine;

public class PathPoint : MonoBehaviour
{
    [SerializeField] private List<PathPointData> pathPoints = new();
    private int currentIndex = 0;

    private void Awake()
    {
        RefreshPathPoints();
    }

    private void OnValidate()
    {
        RefreshPathPoints();
    }

    private void RefreshPathPoints()
    {
        List<PathPointData> existingPoints = pathPoints ?? new List<PathPointData>();
        Transform[] children = GetComponentsInChildren<Transform>(true);

        pathPoints = new List<PathPointData>(children.Length - 1);

        foreach (Transform child in children)
        {
            if (child == transform)
                continue;

            PathPointData existingPoint = existingPoints.Find(point => point != null && point.Transform == child);
            pathPoints.Add(existingPoint ?? new PathPointData
            {
                Transform = child,
                Radius = 0.1f
            });
        }
    }

    public PathPointData CheckNextPoint(Vector3 hitPosition)
    {
        if (currentIndex >= pathPoints.Count)
            return null;

        PathPointData point = pathPoints[currentIndex];

        if (point == null || point.Transform == null)
            return null;

        float distance = Vector3.Distance(
            hitPosition,
            point.Transform.position
        );

        if (distance > point.Radius)
            return null;

        currentIndex++;
        return point;
    }

    #region API
    public List<PathPointData> GetPathPoints => pathPoints;
    public int CurrentIndex => currentIndex;
    public bool IsCompleted => currentIndex >= pathPoints.Count;
    #endregion
}

[Serializable]
public class PathPointData
{
    public Transform Transform;
    public float Radius;
}
