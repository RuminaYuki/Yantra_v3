using UnityEngine;

[CreateAssetMenu(fileName = "NewCDTimer_Modifier",
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Condition/Set Timer Duration")]
public class SetTimerDurationModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private CountDownTimerConditionSO _target;
    [SerializeField] private float _minDuration;
    [SerializeField] private float _maxDuration;

    public void Apply()
    {
        _target.MinDuration = _minDuration;
        _target.MaxDuration = _maxDuration;
    }
}
