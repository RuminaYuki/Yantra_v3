using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "NewOnDead_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Health/On Dead")]
public class OnDeadConditionSO : StateConditionSO
{
    public override Condition CreateCondition()
    {
        return new OnDeadCondition();
    }
}

// Being dead is a state, not a one-off event: it reads Health.IsDead instead of listening
// for OnDead, so it can't be missed or used up by a transition that didn't fire, and it
// stays true for as long as the character is dead (false again after a revive).
public class OnDeadCondition : Condition
{
    private Health _health;

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _health, this);
    }

    protected override bool Statement()
    {
        return _health.IsDead;
    }
}
