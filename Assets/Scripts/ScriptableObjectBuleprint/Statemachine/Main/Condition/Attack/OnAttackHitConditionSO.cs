using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(AttackSphereCast))]
[CreateAssetMenu(
    fileName = "NewOnAttackHit_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Attack/On Attack Hit")]
public class OnAttackHitConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new OnAttackHitCondition();
    }
}

// True once this owner's AttackSphereCast has hit something since entering the state,
// and stays true until the state is entered again.
public class OnAttackHitCondition : Condition
{
    private AnchorReference<AttackSphereCast> _attackSphereCast;
    private AttackSphereCast _subscribed;   // the one we are listening to right now
    private bool _hasHit;

    public override void Awake(StateMachine stateMachine)
    {
        _attackSphereCast = new AnchorReference<AttackSphereCast>(stateMachine, this);
        SyncSubscription();
    }

    public override void OnStateEnter()
    {
        SyncSubscription();
        // A hit from before this state doesn't count.
        _hasHit = false;
    }

    protected override void OnTick()
    {
        SyncSubscription();
    }

    // Follows the anchor: if it now points at another AttackSphereCast (or one appeared late), move the listener.
    private void SyncSubscription()
    {
        _attackSphereCast.TryGet(out AttackSphereCast current);
        if (current == _subscribed)
            return;

        if (_subscribed != null)
            _subscribed.OnHit -= HandleHit;

        _subscribed = current;

        if (_subscribed != null)
            _subscribed.OnHit += HandleHit;
    }

    protected override bool Statement()
    {
        return _hasHit;
    }

    public override void Dispose()
    {
        if (_subscribed != null)
            _subscribed.OnHit -= HandleHit;
        _subscribed = null;
    }

    private void HandleHit()
    {
        _hasHit = true;
    }
}
