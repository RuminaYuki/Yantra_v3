using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Sets the locomotion's Move Mode when the state starts. The mode stays after the state ends,
// unless Restore On Exit is ticked (then the mode from before this state comes back).
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "NewSetMoveMode_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Set Move Mode")]
public class SetMoveModeActionSO : StateActionSO
{
    [SerializeField] private LocomotionMoveMode _mode = LocomotionMoveMode.Velocity;
    [Tooltip("Put the mode back to what it was before this state, when the state ends.")]
    [SerializeField] private bool _restoreOnExit = false;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetMoveModeAction(_mode, _restoreOnExit);
    }
}

public class SetMoveModeAction : StateAction
{
    private readonly LocomotionMoveMode _mode;
    private readonly bool _restoreOnExit;
    private AnchorReference<BaseLocomotion> _locomotion;
    private LocomotionMoveMode _previousMode;

    public SetMoveModeAction(LocomotionMoveMode mode, bool restoreOnExit)
    {
        _mode = mode;
        _restoreOnExit = restoreOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _locomotion = new AnchorReference<BaseLocomotion>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        _previousMode = locomotion.MoveMode;
        locomotion.MoveMode = _mode;
    }

    public override void OnStateExit()
    {
        if (!_restoreOnExit) return;
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        locomotion.MoveMode = _previousMode;
    }

    public override void OnUpdate() { }
}
