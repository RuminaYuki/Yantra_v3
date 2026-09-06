using UnityEngine;

public interface IPathfinder
{
    bool TryCalculatePath(Vector3 start, Vector3 destination, out Vector3 resolvedDestination);
    Vector3 GetDirection(Vector3 currentPosition);
    bool HasReachedDestination();
}