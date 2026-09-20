using UnityEngine;

[CreateAssetMenu(fileName = "NewRange_Modifier", 
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Condition/SetDistanceModifier")]
public class SetDistanceModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private DistanceConditionSO _target;
    [SerializeField] private float _distance;
    public void Apply()
    {
        _target.Distance = _distance;
    }
}
