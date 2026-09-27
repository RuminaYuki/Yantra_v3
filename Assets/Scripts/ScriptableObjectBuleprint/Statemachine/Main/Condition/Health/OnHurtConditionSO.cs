using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

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

public class OnHurtCondition : Condition
{
    private GameObject _owner;
    private Health _health;
    private bool _wasRaised;

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner;
        if (!stateMachine.TryGetRequired(out _health, this))
        {
            return;
        }

        _health.OnHurt += HandleHurt;
    }

    public override void OnStateEnter()
    {
        _wasRaised = false;
    }

    protected override bool Statement()
    {
        bool result = _wasRaised;
        _wasRaised = false;
        return result;
    }

    public override void Dispose()
    {
        if (_health != null)
            _health.OnHurt -= HandleHurt;

        _wasRaised = false;
    }

    private void HandleHurt()
    {
        _wasRaised = true;
    }
}
