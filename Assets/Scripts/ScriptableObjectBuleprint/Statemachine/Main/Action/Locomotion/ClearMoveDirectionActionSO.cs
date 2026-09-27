using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "ClearMoveDirectionAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Clear Move Direction")]
public class ClearMoveDirectionActionSO : StateActionSO
{
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new ClearMoveDirectionAction();
    }
}

public class ClearMoveDirectionAction : StateAction
{
    private BaseLocomotion _locomotion;

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _locomotion, this);
    }

    public override void OnStateEnter()
    {
        _locomotion.ClearMovementDirection();
    }

    public override void OnUpdate() { }
}
