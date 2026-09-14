using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "LookInputPlayerCamera_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Camera/LookInputPlayerCamera")]

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
        if (_cameraAnchor != null)
        {
            if (_cameraAnchor.Value == null)
            {
                Debug.LogWarning(
                    $"{_cameraAnchor.name} has not been provided a value yet; LookInputPlayerCameraAction cannot resolve a PlayerCameraController."
                );
                return;
            }
            _playerCameraController = _cameraAnchor.Value.gameObject.GetComponent<PlayerCameraController>();
            if (_playerCameraController == null)
            {
                Debug.LogWarning(
                    $"PlayerCameraController component was not found on the GameObject referenced by {_cameraAnchor.name}."
                );
            }
            return;
        }
        _playerCameraController = stateMachine.GetComponent<PlayerCameraController>();
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
