using UnityEngine;

public class PathGizmos : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _homePoint;
    [SerializeField] private bool _showGizmos = true;
    [SerializeField] private Color _arrowColor = Color.cyan;
    [SerializeField, Min(0.05f)] private float _arrowHeadLength = 0.4f;
    [SerializeField, Range(5f, 60f)] private float _arrowHeadAngle = 20f;

    private void OnDrawGizmos()
    {
        if (!_showGizmos)
            return;

        Transform player = _target != null ? _target : FindPlayer();
        Transform root = _homePoint != null ? _homePoint : transform;

        if (player == null || root == null)
            return;

        Gizmos.color = _arrowColor;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            DrawArrow(player.position, child.position);
        }
    }

    private Transform FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    private void DrawArrow(Vector3 from, Vector3 to)
    {
        Gizmos.DrawLine(from, to);

        Vector3 direction = (to - from).normalized;
        if (direction == Vector3.zero)
            return;

        Quaternion rotation = Quaternion.LookRotation(direction);
        Vector3 right = rotation * Quaternion.Euler(0f, 180f + _arrowHeadAngle, 0f) * Vector3.forward;
        Vector3 left = rotation * Quaternion.Euler(0f, 180f - _arrowHeadAngle, 0f) * Vector3.forward;

        Gizmos.DrawLine(to, to + right * _arrowHeadLength);
        Gizmos.DrawLine(to, to + left * _arrowHeadLength);
    }
}