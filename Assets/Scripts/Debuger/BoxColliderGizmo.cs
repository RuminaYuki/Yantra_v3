using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BoxColliderGizmo : MonoBehaviour
{
    [SerializeField] private Color _color = new Color(0f, 1f, 0f, 0.25f);
    [SerializeField] private bool _drawSolid = true;

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        Gizmos.matrix = transform.localToWorldMatrix;   // ให้หมุนและ scale ตาม object
        Gizmos.color = _color;
        if (_drawSolid) Gizmos.DrawCube(box.center, box.size);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
