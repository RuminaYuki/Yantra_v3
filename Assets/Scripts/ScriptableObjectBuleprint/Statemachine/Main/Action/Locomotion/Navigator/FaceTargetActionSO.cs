using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "FaceTarget_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Face Target")]
public class FaceTargetActionSO : StateActionSO
{
    [SerializeField] private GameObjectAnchor _targetAnchor;
    [SerializeField] private float _rotateSpeed = 1f;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new FaceTargetAction(_targetAnchor,_rotateSpeed);
    }
}

public class FaceTargetAction : StateAction
{
    private readonly GameObjectAnchor _targetAnchor;
    private readonly float _rotateSpeed;
    private BaseLocomotion _locomotion;
    private float _previousRotateSpeed;
    private Transform _owner;

    public FaceTargetAction(GameObjectAnchor targetAnchor, float rotateSpeed)
    {
        _targetAnchor = targetAnchor;
        _rotateSpeed = rotateSpeed;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner.transform;
        stateMachine.TryGetRequired(out _locomotion, this);

        if (_targetAnchor == null)
            Debug.LogError("FaceTargetAction has no target GameObjectAnchor assigned.");
    }

    public override void OnStateEnter()
    {
        _previousRotateSpeed = _locomotion.GetRotateSmoothSpeed();
        _locomotion.SetRotateSmoothSpeed(_rotateSpeed);
    }

    public override void OnUpdate()
    {
        if (_owner == null)
            return;

        if (_targetAnchor == null || !_targetAnchor.IsSet || _targetAnchor.Value == null)
            return;

        Vector3 direction = _targetAnchor.Value.transform.position - _owner.position;
        _locomotion.SetFacingDirection(direction);
    }

    public override void OnStateExit()
    {
        _locomotion.SetRotateSmoothSpeed(_previousRotateSpeed);
        _locomotion.ClearFacingDirection();
    }

}
