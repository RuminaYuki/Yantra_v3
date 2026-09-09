using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewSubStateMachine_Action",
    menuName = "YUKI Learning State Machine/StateMachine/SubStateMachine/Actions/SubStateMachineAction")]
public class SubStateMachineActionSO : StateActionSO
{
    [SerializeField] private TransitionTableSO _transitionTable;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SubStateMachineAction(_transitionTable);
    }
}

public class SubStateMachineAction : StateAction
{
    private readonly TransitionTableSO _transitionTable;

    private StateMachine _parentStateMachine;
    private StateMachine _childStateMachine;
    private bool _skipNextUpdate;

    public SubStateMachineAction(TransitionTableSO transitionTable)
    {
        _transitionTable = transitionTable;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _parentStateMachine = stateMachine;
    }

    public override void OnStateEnter()
    {
        if (_transitionTable == null)
        {
            Debug.LogError(
                "SubStateMachineAction does not have a TransitionTableSO.",
                _parentStateMachine.Owner);
            return;
        }

        if (_childStateMachine != null)
        {
            DisposeChildStateMachine();
        }

        _childStateMachine = new StateMachine(
            _parentStateMachine.Owner);

        _childStateMachine.Exited += OnChildExited;
        _childStateMachine.StateChanged += OnChildStateChanged;

        State initialState = _transitionTable.CreateInitialState(
            _childStateMachine);

        _childStateMachine.SetInitialState(initialState);
        
        _skipNextUpdate = true;
    }

    public override void OnUpdate()
    {
        if (_skipNextUpdate)
        {
            _skipNextUpdate = false;
            return;
        }

        _childStateMachine?.OnUpdate();
    }

    public override void OnFixedUpdate()
    {
        _childStateMachine?.OnFixedUpdate();
    }

    public override void OnStateExit()
    {
        DisposeChildStateMachine();
    }

    private void OnChildExited(string exitId)
    {
        _parentStateMachine.NotifyChildStateMachineExited(exitId);
    }

    private void OnChildStateChanged(string previousStateName, string currentStateName)
    {
        _parentStateMachine.NotifyChildStateChanged(previousStateName, currentStateName);
    }

    private void DisposeChildStateMachine()
    {
        if (_childStateMachine == null)
        {
            return;
        }
        _childStateMachine.Exited -= OnChildExited;
        _childStateMachine.StateChanged -= OnChildStateChanged;

        _childStateMachine.Dispose();
        _childStateMachine = null;
    }
}
