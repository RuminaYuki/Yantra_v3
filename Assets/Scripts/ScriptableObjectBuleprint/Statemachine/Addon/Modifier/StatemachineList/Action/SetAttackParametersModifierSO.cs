using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackPara_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/Set Attack Parameters")]
public class SetAttackParametersModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private ExecuteAttackActionSO _target;
    [SerializeField] private AttackParameters _parameters;

    public void Apply()
    {
        _target.AttackParameters = _parameters;
    }
}
