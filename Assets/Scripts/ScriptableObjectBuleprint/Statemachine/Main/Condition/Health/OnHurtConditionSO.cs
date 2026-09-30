using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "NewOnHurt_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Health/On Hurt")]
public class OnHurtConditionSO : StateConditionSO
{
    public override Condition CreateCondition()
    {
        return new OnHurtCondition();
    }
}

// A hit is true for exactly one state machine update (the one right after it happened),
// however many transitions read it and in whatever order. It isn't used up by reading,
// so a transition that doesn't fire can't swallow it, and it can't linger for later.
public class OnHurtCondition : Condition
{
    private Health _health;
    private bool _pending;   // hit happened, waiting for the next update
    private bool _active;    // true during this update

    public override void Awake(StateMachine stateMachine)
    {
        if (!stateMachine.TryGetRequired(out _health, this))
        {
            return;
        }

        _health.OnHurt += HandleHurt;
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
        _active = _pending;
        _pending = false;
    }

    protected override bool Statement()
    {
        return _active;
    }

    public override void Dispose()
    {
        if (_health != null)
            _health.OnHurt -= HandleHurt;
    }

    private void HandleHurt()
    {
        _pending = true;
    }
}
