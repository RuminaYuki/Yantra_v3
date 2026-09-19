using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

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
        _locomotion = stateMachine.GetComponent<BaseLocomotion>();

        if (_locomotion == null)
            Debug.LogError("ClearMoveDirectionAction cannot find BaseLocomotion.");
    }

    public override void OnStateEnter()
    {
        _locomotion?.ClearMovementDirection();
    }

    public override void OnUpdate() { }
}
