using UnityEngine;

// Answers one question: can this character stand at a point?
// How the point is picked (ring, offset, ...) belongs to the caller.
public class StandablePointValidator : MonoBehaviour
{
    [SerializeField] private float _bodyRadius = 0.4f;
    [SerializeField] private LayerMask _obstacleLayer;
    [SerializeField] private LayerMask _groundLayer;

    public float BodyRadius => Mathf.Max(0.01f, _bodyRadius);
    public LayerMask ObstacleLayer => _obstacleLayer;
    public LayerMask GroundLayer => _groundLayer;

    public bool TryValidatePoint(Vector3 origin, Vector3 candidate, out Vector3 groundPoint)
    {
        float bodyRadius = BodyRadius;
        Vector3 pathStart = origin + Vector3.up * bodyRadius;
        Vector3 pathEnd = candidate + Vector3.up * bodyRadius;

        // Prevent the point from crossing an obstacle.
        if (Physics.Linecast(
                pathStart,
                pathEnd,
                _obstacleLayer,
                QueryTriggerInteraction.Ignore))
        {
            groundPoint = default;
            return false;
        }

        if (!TryGetGround(candidate, out groundPoint))
            return false;

        // Prevent the destination from overlapping an obstacle.
        Vector3 overlapCenter =
            groundPoint + Vector3.up * bodyRadius;

        if (Physics.CheckSphere(
                overlapCenter,
                bodyRadius,
                _obstacleLayer,
                QueryTriggerInteraction.Ignore))
        {
            groundPoint = default;
            return false;
        }

        return true;
    }

    private bool TryGetGround(Vector3 position, out Vector3 groundPoint)
    {
        Vector3 origin = position + Vector3.up * 2f;

        if (Physics.Raycast(origin, Vector3.down,
                out RaycastHit hit, 4f, _groundLayer))
        {
            groundPoint = hit.point;
            return true;
        }

        groundPoint = default;
        return false;
    }
}
