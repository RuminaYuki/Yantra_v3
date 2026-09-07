using System;
using System.Collections.Generic;
using UnityEngine;

public class PathPoint : MonoBehaviour
{
    [SerializeField] private List<PathPointData> pathPoints = new();
    private int currentIndex = 0;

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
