using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackDamageParameter_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/Set Attack Damage Parameter")]
public class SetAttackDamageParameterModifierSO : ScriptableObject, IStatModifier, IGizmoModule
{
    [SerializeField] private SetAttackDamageParameterActionSO _target;
    [SerializeField] private AttackParameters _parameters;
    [SerializeField] private DamageTypeID _damageType;
    [Header("Gizmos")]
    [SerializeField] private bool _enabledGizmos = false;
    [SerializeField] private Color _gizmoColor = Color.yellow;
    public bool GizmoEnabled => _enabledGizmos;
    public void Apply()
    {
        _target.AttackParameters = _parameters;
        _target.DamageType = _damageType;
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
