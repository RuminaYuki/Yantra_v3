using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "ClearMoveDirectionAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Clear Move Direction")]
public class ClearMoveDirectionActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new ClearMoveDirectionAction();
    }
}

public class ClearMoveDirectionAction : StateAction
{
    private AnchorReference<BaseLocomotion> _locomotion;

    public override void Awake(StateMachine stateMachine)
    {
        _locomotion = new AnchorReference<BaseLocomotion>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        locomotion.ClearMovementDirection();
    }

    public override void OnUpdate() { }
}
