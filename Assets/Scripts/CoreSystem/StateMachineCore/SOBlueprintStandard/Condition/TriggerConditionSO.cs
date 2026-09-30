using System.Collections.Generic;
using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewTrigger_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Trigger")]
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

// A trigger is true for exactly one state machine update (the one right after Trigger()),
// however many transitions read it and in whatever order. It isn't used up by reading,
// so a transition that doesn't fire can't swallow it, and it can't linger for later.
public class TriggerCondition : Condition
{
    private readonly TriggerConditionSO _owner;
    private bool _pending;   // triggered, waiting for the next update
    private bool _active;    // true during this update

    public TriggerCondition(TriggerConditionSO owner)
    {
        _owner = owner;
    }

    public void SetTriggered() => _pending = true;

    // A trigger from before this state doesn't count.
    public override void OnStateEnter()
    {
        _pending = false;
        _active = false;
    }

    // Start of every update: last update's trigger expires, a new one becomes visible.
    protected override void OnTick()
    {
        _active = _pending;
        _pending = false;
    }

    protected override bool Statement() => _active;

    public override void Dispose() => _owner.Unregister(this);
}
