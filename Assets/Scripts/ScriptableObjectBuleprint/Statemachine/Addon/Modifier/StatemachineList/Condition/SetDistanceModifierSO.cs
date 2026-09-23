using UnityEngine;

[CreateAssetMenu(fileName = "NewRange_Modifier", 
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Condition/SetDistanceModifier")]
public class SetDistanceModifierSO : ScriptableObject, IStatModifier, IGizmoModule
{
    [SerializeField] private DistanceConditionSO _target;
    [SerializeField] private float _distance;
    [Header("Gizmos")]
    [SerializeField] private bool _enabledGizmos = false;
    [SerializeField] private Color _gizmocolor = Color.yellow;
    public bool GizmoEnabled => _enabledGizmos;
    public void Apply()
    {
        _target.Distance = _distance;
    }
    public void DrawGizmos(Transform origin)
    {
        Gizmos.color = _gizmocolor;
        Gizmos.DrawWireSphere(origin.position, _distance);
    }
}
