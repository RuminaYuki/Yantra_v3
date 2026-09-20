using UnityEngine;

[CreateAssetMenu(fileName = "NewExecuteAttackPara_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/Set Execute Attack Parameters")]
public class SetExecuteAttackParametersModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private ExecuteAttackActionSO _target;
    [SerializeField] private AttackParameters _parameters;

    public void Apply()
    {
        _target.AttackParameters = _parameters;
    }
}
