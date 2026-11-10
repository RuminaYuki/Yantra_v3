using UnityEngine;

public class RangeGizmo : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _range = 1f;
    [SerializeField] private Color _color = new Color(0f, 1f, 0f, 0.25f);
    [SerializeField] private bool _drawSolid = true;

    public float Range => _range;

    private void OnDrawGizmos()
    {
        Gizmos.color = _color;
        if (_drawSolid) Gizmos.DrawSphere(transform.position, _range);
        Gizmos.DrawWireSphere(transform.position, _range);
    }
}
