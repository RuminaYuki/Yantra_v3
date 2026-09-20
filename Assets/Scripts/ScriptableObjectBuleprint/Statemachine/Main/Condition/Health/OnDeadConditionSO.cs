using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

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

public class OnDeadCondition : Condition
{
    private GameObject _owner;
    private Health _health;
    private bool _wasRaised;

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner;
        _health = _owner.GetComponent<Health>();

        if (_health == null)
        {
            Debug.LogError("OnDeadCondition requires a Health component on the owner.", _owner);
            return;
        }

        _health.OnDead += HandleDead;
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
            _health.OnDead -= HandleDead;

        _wasRaised = false;
    }

    private void HandleDead()
    {
        _wasRaised = true;
    }
}
