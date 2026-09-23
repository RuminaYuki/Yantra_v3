using UnityEngine;

[CreateAssetMenu(fileName = "NewSetRanPosNav_ModifierSO",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/SetRandomPositionNavRadiusModifier")]
public class SetRandomPositionNavRadiusModifierSO : ScriptableObject, IStatModifier, IGizmoModule
{
    [SerializeField] private SetRandomPositionNavRadiusActionSO _target;
    [SerializeField] private float _radius;
    [SerializeField] private float _minDistance;
    [Header("Gizmos")]
    [SerializeField] private bool _enabledGizmos = false;
    [SerializeField] private Color _gizmoRadiusColor = Color.yellow;
    [SerializeField] private Color _gizmoMinDistanceColor = Color.red;
    public bool GizmoEnabled => _enabledGizmos;
    public void Apply()
    {
        _target.Radius = _radius;
        _target.MinDistance = _minDistance;
    }
    public void DrawGizmos(Transform origin)
    {
        Gizmos.color = _gizmoRadiusColor;
        WaypointPath waypointPath = origin.GetComponent<WaypointPath>();
        Gizmos.DrawWireSphere(waypointPath.PathRoot.position, _radius);
        Gizmos.color = _gizmoMinDistanceColor;
        Gizmos.DrawWireSphere(waypointPath.PathRoot.position, _minDistance);
    }
}
