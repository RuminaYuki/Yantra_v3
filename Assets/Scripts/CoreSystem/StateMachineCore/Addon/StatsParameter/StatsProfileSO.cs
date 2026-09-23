using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Character_Stats", menuName = "YUKI Learning State Machine/StatsParameter/StatsProfileSO")]
public class StatsProfileSO : ScriptableObject
{
    [SerializeField] private List<ScriptableObject> _modifiers;

    public void ApplyStats()
    {
        foreach (var modifier in _modifiers)
            (modifier as IStatModifier)?.Apply();
    }
    public void DrawGizmos(Transform origin)
    {
        foreach (var modifier in _modifiers)
        {
            if (modifier is IGizmoModule gizmoModule && gizmoModule.GizmoEnabled)
                gizmoModule.DrawGizmos(origin);
        }
    }
}
