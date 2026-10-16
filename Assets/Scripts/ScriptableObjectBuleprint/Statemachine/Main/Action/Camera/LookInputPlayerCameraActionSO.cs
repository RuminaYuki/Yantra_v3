using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PlayerCameraController))]
[CreateAssetMenu(
    fileName = "LookInputPlayerCamera_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Camera/LookInputPlayerCamera")]

public class LookInputPlayerCameraActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new LookInputPlayerCameraAction();
    }
}

public class LookInputPlayerCameraAction : StateAction
{
    private AnchorReference<PlayerCameraController> _playerCameraController;

    public override void Awake(StateMachine stateMachine)
    {
        _playerCameraController = new AnchorReference<PlayerCameraController>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        if (!_playerCameraController.TryGet(out PlayerCameraController cameraController)) return;
        cameraController.IsLookLocked = true;
    }
    public override void OnUpdate(){}

    public override void OnStateExit()
    {
        if (!_playerCameraController.TryGet(out PlayerCameraController cameraController)) return;
        cameraController.IsLookLocked = false;
    }
}
