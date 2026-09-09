using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewExitSubStateMachine_Action",
    menuName = "YUKI Learning State Machine/StateMachine/SubStateMachine/Actions/ExitStateAction")]
public class ExitSubStateMachineActionSO : StateActionSO
{
    [SerializeField] private string _exitId = "Default";

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new ExitSubStateMachineAction(_exitId);
    }
}

public class ExitSubStateMachineAction : StateAction
{
    private readonly string _exitId;
    private StateMachine _stateMachine;

    public ExitSubStateMachineAction(string exitId)
    {
        _exitId = exitId;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _stateMachine = stateMachine;
    }

    public override void OnStateEnter()
    {
        _stateMachine.RequestExit(_exitId);
    }

    public override void OnUpdate()
    {
    }
}