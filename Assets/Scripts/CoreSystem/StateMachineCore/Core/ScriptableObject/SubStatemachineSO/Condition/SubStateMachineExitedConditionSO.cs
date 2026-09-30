using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewSubStateMachineExited_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/SubStateMachine/Sub State Machine Exited")]
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

    // "The child has exited" is a state, not a one-off event: once the child machine has
    // exited it sends nothing more, so reading must not clear it, or a transition that
    // reads it but doesn't fire would leave this state stuck forever.
    // Cleared only in OnStateEnter, when the child machine is started again.
    protected override bool Statement()
    {
        return _exitReceived;
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
