using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewSubStateMachine_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Addon/SubStateMachine/Actions/SubStateMachineAction")]
public class SubStateMachineActionSO : StateActionSO
{
    [SerializeField] private TransitionTableSO _transitionTable;

    [Tooltip("ถ้าตั้งไว้ — ทุกครั้งที่ออกจาก state นี้ (ไม่ว่าจะออกแบบปกติหรือถูก parent บังคับออกกะทันหัน) จะวิ่งผ่าน state นี้ก่อนเสมอ ก่อน dispose child machine เช่น PlayerPutGunDown_State เพื่อให้ animation เก็บปืนได้เล่นทุกครั้ง")]
    [SerializeField] private StateSO _forceExitState;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SubStateMachineAction(_transitionTable, _forceExitState);
    }
}

public class SubStateMachineAction : StateAction
{
    private readonly TransitionTableSO _transitionTable;
    private readonly StateSO _forceExitStateSO;

    private StateMachine _parentStateMachine;
    private StateMachine _childStateMachine;
    private State _forceExitState;
    private bool _skipNextUpdate;

    public SubStateMachineAction(TransitionTableSO transitionTable, StateSO forceExitState)
    {
        _transitionTable = transitionTable;
        _forceExitStateSO = forceExitState;
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

        _childStateMachine = new StateMachine(_parentStateMachine.Owner);
        _childStateMachine.Exited += OnChildExited;
        _childStateMachine.StateChanged += OnChildStateChanged;

        State initialState = _transitionTable.CreateInitialState(
            _childStateMachine, out var allStates);

        _forceExitState = _forceExitStateSO != null &&
            allStates.TryGetValue(_forceExitStateSO, out State exitState)
                ? exitState
                : null;

        _childStateMachine.SetInitialState(initialState);
        _skipNextUpdate = true;
    }

    public override void OnUpdate()
    {
        if (_skipNextUpdate) { _skipNextUpdate = false; return; }
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
        => _parentStateMachine.NotifyChildStateMachineExited(exitId);

    private void OnChildStateChanged(string previousStateName, string currentStateName)
        => _parentStateMachine.NotifyChildStateChanged(previousStateName, currentStateName);

    private void DisposeChildStateMachine()
    {
        if (_childStateMachine == null) return;

        // บังคับวิ่งผ่าน exit state ก่อนเสมอ (ถ้าตั้งไว้ และยังไม่ได้อยู่ตรงนั้น)
        // ChangeState เองมี guard กันเข้า state เดิมซ้ำอยู่แล้ว (ReferenceEquals check)
        if (_forceExitState != null)
        {
            _childStateMachine.ChangeState(_forceExitState);
        }

        _childStateMachine.Exited -= OnChildExited;
        _childStateMachine.StateChanged -= OnChildStateChanged;
        _childStateMachine.Dispose();
        _childStateMachine = null;
    }
}
