using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "NewOnHurt_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Health/On Hurt")]
public class OnHurtConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new OnHurtCondition();
    }
}

// A hit is true for exactly one state machine update (the one right after it happened),
// however many transitions read it and in whatever order. It isn't used up by reading,
// so a transition that doesn't fire can't swallow it, and it can't linger for later.
public class OnHurtCondition : Condition
{
    private AnchorReference<Health> _health;
    private Health _subscribed;   // the Health we are listening to right now
    private bool _pending;   // hit happened, waiting for the next update
    private bool _active;    // true during this update

    public override void Awake(StateMachine stateMachine)
    {
        _health = new AnchorReference<Health>(stateMachine, this);
        SyncSubscription();
    }

    // Follows the anchor: if it now points at another Health (or one appeared late), move the listener.
    private void SyncSubscription()
    {
        _health.TryGet(out Health current);
        if (current == _subscribed)
            return;

        if (_subscribed != null)
            _subscribed.OnHurt -= HandleHurt;

        _subscribed = current;

        if (_subscribed != null)
            _subscribed.OnHurt += HandleHurt;
    }

    public override void OnStateEnter()
    {
        // A hit from before this state (e.g. the one that got us here) doesn't count.
        _pending = false;
        _active = false;
    }

    // Start of every update: last update's hit expires, a new one becomes visible.
    protected override void OnTick()
    {
        SyncSubscription();
        _active = _pending;
        _pending = false;
    }

    protected override bool Statement()
    {
        return _active;
    }

    public override void Dispose()
    {
        if (_subscribed != null)
            _subscribed.OnHurt -= HandleHurt;
        _subscribed = null;
    }

    private void HandleHurt()
    {
        _pending = true;
    }
}
