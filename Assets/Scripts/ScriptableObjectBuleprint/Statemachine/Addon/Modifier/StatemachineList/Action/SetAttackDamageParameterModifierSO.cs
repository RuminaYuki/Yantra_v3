using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackDamageParameter_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/Set Attack Damage Parameter")]
public class SetAttackDamageParameterModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private SetAttackDamageParameterActionSO _target;
    [SerializeField] private AttackParameters _parameters;
    [SerializeField] private DamageTypeID _damageType;

    public void Apply()
    {
        _target.AttackParameters = _parameters;
        _target.DamageType = _damageType;
    }
}
