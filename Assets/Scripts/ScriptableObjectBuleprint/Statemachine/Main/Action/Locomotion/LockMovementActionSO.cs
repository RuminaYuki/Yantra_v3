using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "LockMovementAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Lock Movement")]
public class LockMovementActionSO : StateActionSO
{
    [SerializeField] private bool _resetMoveAnimation = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new LockMovementAction(_resetMoveAnimation);
    }
}

public class LockMovementAction : StateAction
{
    private readonly bool _resetMoveAnimation;
    private AnchorReference<BaseLocomotion> _locomotion;

    public LockMovementAction(bool resetMoveAnimation)
    {
        _resetMoveAnimation = resetMoveAnimation;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _locomotion = new AnchorReference<BaseLocomotion>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        locomotion.LockLocomotion(
            this,
            _resetMoveAnimation);
    }

    public override void OnStateExit()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        locomotion.UnlockLocomotion(this);
    }

    public override void OnUpdate() { }
}
