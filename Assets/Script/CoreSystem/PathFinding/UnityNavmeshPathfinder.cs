using UnityEngine;
using UnityEngine.AI;

public class UnityNavMeshPathfinder : IPathfinder
{
    private const float SampleRadius = 1f;

    private readonly NavMeshPath _path = new();

    private int _cornerIndex = 1;

    public bool TryCalculatePath(
        Vector3 start,
        Vector3 destination,
        out Vector3 resolvedDestination)
    {
        resolvedDestination = default;

        if (!NavMesh.SamplePosition(
                destination,
                out NavMeshHit destinationHit,
                SampleRadius,
                NavMesh.AllAreas))
        {
            ClearPath();
            return false;
        }

        if (!NavMesh.SamplePosition(
                start,
                out NavMeshHit startHit,
                SampleRadius,
                NavMesh.AllAreas))
        {
            ClearPath();
            return false;
        }

        bool success = NavMesh.CalculatePath(
            startHit.position,
            destinationHit.position,
            NavMesh.AllAreas,
            _path);

        _cornerIndex = 1;

        if (!success ||
            _path.status != NavMeshPathStatus.PathComplete ||
            _path.corners.Length < 2)
        {
            ClearPath();
            return false;
        }

        resolvedDestination = destinationHit.position;
        return true;
    }

    public Vector3 GetDirection(
        Vector3 currentPosition)
    {
        if (_path.corners == null ||
            _cornerIndex >= _path.corners.Length)
        {
            return Vector3.zero;
        }

        Vector3 targetCorner =
            _path.corners[_cornerIndex];

        Vector3 direction =
            targetCorner - currentPosition;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.1f)
        {
            _cornerIndex++;

            if (_cornerIndex >= _path.corners.Length)
                return Vector3.zero;

            targetCorner =
                _path.corners[_cornerIndex];

            direction =
                targetCorner - currentPosition;

            direction.y = 0f;
        }

        return direction.normalized;
    }

    public bool HasReachedDestination()
    {
        return _cornerIndex >= _path.corners.Length;
    }

    private void ClearPath()
    {
        _path.ClearCorners();
        _cornerIndex = 1;
    }
}