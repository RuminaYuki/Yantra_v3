using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(AttackSphereCast))]
[CreateAssetMenu(
    fileName = "NewOnAttackHit_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Attack/On Attack Hit")]
public class OnAttackHitConditionSO : StateConditionSO
{
    public override Condition CreateCondition()
    {
        return new OnAttackHitCondition();
    }
}

// True once this owner's AttackSphereCast has hit something since entering the state,
// and stays true until the state is entered again.
public class OnAttackHitCondition : Condition
{
    private AttackSphereCast _attackSphereCast;
    private bool _hasHit;

    public override void Awake(StateMachine stateMachine)
    {
        if (!stateMachine.TryGetRequired(out _attackSphereCast, this))
        {
            return;
        }

        _attackSphereCast.OnHit += HandleHit;
    }

    public override void OnStateEnter()
    {
        // A hit from before this state doesn't count.
        _hasHit = false;
    }

    protected override bool Statement()
    {
        return _hasHit;
    }

    public override void Dispose()
    {
        if (_attackSphereCast != null)
            _attackSphereCast.OnHit -= HandleHit;
    }

    private void HandleHit()
    {
        _hasHit = true;
    }
}
