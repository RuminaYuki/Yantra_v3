using System.Collections.Generic;
using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewTrigger_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/Trigger")]
public class TriggerConditionSO : StateConditionSO
{
    private readonly List<TriggerCondition> _runtimeConditions = new();

    public override Condition CreateCondition()
    {
        var condition = new TriggerCondition(this);
        _runtimeConditions.Add(condition);
        return condition;
    }

    public void Trigger()
    {
        foreach (TriggerCondition condition in _runtimeConditions)
        {
            condition.SetTriggered();
        }
    }

    public void Unregister(TriggerCondition condition)
    {
        _runtimeConditions.Remove(condition);
    }
}

public class TriggerCondition : Condition
{
    private readonly TriggerConditionSO _owner;
    private bool _wasTriggered;

    public TriggerCondition(TriggerConditionSO owner)
    {
        _owner = owner;
    }

    public void SetTriggered() => _wasTriggered = true;

    public override void OnStateEnter() => _wasTriggered = false;

    protected override bool Statement()
    {
        bool result = _wasTriggered;
        _wasTriggered = false;
        return result;
    }

    public override void Dispose() => _owner.Unregister(this);
}
