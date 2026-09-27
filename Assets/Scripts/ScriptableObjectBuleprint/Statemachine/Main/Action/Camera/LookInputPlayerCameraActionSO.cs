using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "LookInputPlayerCamera_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Camera/LookInputPlayerCamera")]

public class LookInputPlayerCameraActionSO : StateActionSO
{
    [SerializeField] private GameObjectAnchor _cameraAnchor;
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new LookInputPlayerCameraAction(_cameraAnchor);
    }
}

public class LookInputPlayerCameraAction : StateAction
{
    private readonly GameObjectAnchor _cameraAnchor;
    private PlayerCameraController _playerCameraController;
    public LookInputPlayerCameraAction(GameObjectAnchor cameraAnchor)
    {
        _cameraAnchor = cameraAnchor;
    }
    public override void Awake(StateMachine stateMachine)
    {
        _cameraAnchor.TryGetComponentOrOwner(stateMachine, out _playerCameraController, this);
    }
    public override void OnStateEnter()
    {
        if (_playerCameraController == null) return;
        _playerCameraController.IsLookLocked = true;
    }
    public override void OnUpdate(){}

    public override void OnStateExit()
    {
        if (_playerCameraController == null) return;
        _playerCameraController.IsLookLocked = false;
    }
}
