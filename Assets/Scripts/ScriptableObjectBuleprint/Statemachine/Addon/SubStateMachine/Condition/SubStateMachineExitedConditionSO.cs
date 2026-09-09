using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewSubStateMachineExitedCondition",
    menuName = "YUKI Learning State Machine/StateMachine/SubStateMachine/Conditions/Sub State Machine Exited")]
public class SubStateMachineExitedConditionSO : StateConditionSO
{
    [SerializeField] private string _exitId = "Default";

    public override Condition CreateCondition()
    {
        return new SubStateMachineExitedCondition(_exitId);
    }
}

public class SubStateMachineExitedCondition : Condition
{
    private readonly string _expectedExitId;

    private StateMachine _stateMachine;
    private bool _exitReceived;

    public SubStateMachineExitedCondition(string expectedExitId)
    {
        _expectedExitId = expectedExitId;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _stateMachine = stateMachine;
        _stateMachine.ChildStateMachineExited += OnChildExited;
    }

    public override void OnStateEnter()
    {
        _exitReceived = false;
    }

    protected override bool Statement()
    {
        bool result = _exitReceived;
        _exitReceived = false;
        return result;
    }

    public override void Dispose()
    {
        if (_stateMachine != null)
        {
            _stateMachine.ChildStateMachineExited -= OnChildExited;
            _stateMachine = null;
        }

        _exitReceived = false;
    }

    private void OnChildExited(string exitId)
    {
        if (exitId == _expectedExitId)
        {
            _exitReceived = true;
        }
    }
}
