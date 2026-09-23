using UnityEngine;
public interface IGizmoModule
{
    bool GizmoEnabled {get;}
    void DrawGizmos(Transform origin);
}
