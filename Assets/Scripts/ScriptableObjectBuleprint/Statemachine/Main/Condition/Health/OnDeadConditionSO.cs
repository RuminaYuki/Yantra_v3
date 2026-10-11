using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "NewOnDead_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Health/On Dead")]
public class OnDeadConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new OnDeadCondition();
    }
}

// Being dead is a state, not a one-off event: it reads Health.IsDead instead of listening
// for OnDead, so it can't be missed or used up by a transition that didn't fire, and it
// stays true for as long as the character is dead (false again after a revive).
public class OnDeadCondition : Condition
{
    private AnchorReference<Health> _health;

    public override void Awake(StateMachine stateMachine)
    {
        _health = new AnchorReference<Health>(stateMachine, this);
    }

    protected override bool Statement()
    {
        if (!_health.TryGet(out Health health)) return false;
        return health.IsDead;
    }
}
