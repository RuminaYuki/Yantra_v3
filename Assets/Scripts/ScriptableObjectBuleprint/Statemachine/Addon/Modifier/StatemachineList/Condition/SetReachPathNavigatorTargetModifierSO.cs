using UnityEngine;

[CreateAssetMenu(fileName = "NewReachPathNavigatorTarget_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Condition/Set Reach Path Navigator Target")]
public class SetReachPathNavigatorTargetModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private ReachPathNavigatorTargetConditionSO _target;
    [SerializeField] private float _arrivalDistance;

    public void Apply()
    {
        _target.ArrivalDistance = _arrivalDistance;
    }
}
