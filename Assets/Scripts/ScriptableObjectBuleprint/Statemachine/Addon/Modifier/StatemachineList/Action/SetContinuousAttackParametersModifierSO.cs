using UnityEngine;

[CreateAssetMenu(fileName = "NewContinuousAttackPara_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/Set Continuous Attack Parameters")]
public class SetContinuousAttackParametersModifierSO : ScriptableObject, IStatModifier, IGizmoModule
{
    [SerializeField] private ContinuousAttackActionSO _target;
    [SerializeField] private AttackParameters _parameters;
    [Header("Gizmos")]
    [SerializeField] private bool _enabledGizmos = false;
    [SerializeField] private Color _gizmoColor = Color.green;
    public bool GizmoEnabled => _enabledGizmos;
    public void Apply()
    {
        _target.AttackParameters = _parameters;
    }
    public void DrawGizmos(Transform origin)
    {
        Gizmos.color = _gizmoColor;
        Vector3 start = origin.position;
        Vector3 end = start + origin.forward * _parameters.attackRange;

        Gizmos.DrawWireSphere(start, _parameters.attackRadius);
        Gizmos.DrawWireSphere(end, _parameters.attackRadius);
        Gizmos.DrawLine(start, end);
    }
}
