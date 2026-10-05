using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDelayedCondition_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Condition/Set Delayed Condition")]
public class SetDelayedConditionModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private List<DelayedConditionSO> _target;
    [SerializeField] private float _delay;

    public void Apply()
    {
        foreach (DelayedConditionSO delayedCondition in _target)
        {
            delayedCondition.Delay = _delay;
        }
    }
}
