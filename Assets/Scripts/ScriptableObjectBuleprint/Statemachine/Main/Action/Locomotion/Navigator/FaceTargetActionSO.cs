using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "FaceTarget_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Face Target")]
public class FaceTargetActionSO : StateActionSO
{
    [Tooltip("Required. Self Anchor is the one that turns (empty = Owner).")]
    [SerializeField] private AnchorIDSO _targetAnchor;
    [SerializeField] private float _rotateSpeed = 1f;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new FaceTargetAction(_targetAnchor,_rotateSpeed);
    }
}

public class FaceTargetAction : StateAction
{
    private readonly AnchorIDSO _targetAnchor;
    private readonly float _rotateSpeed;
    private AnchorReference<BaseLocomotion> _locomotion;
    private AnchorReference<Transform> _target;
    private float _previousRotateSpeed;

    public FaceTargetAction(AnchorIDSO targetAnchor, float rotateSpeed)
    {
        _targetAnchor = targetAnchor;
        _rotateSpeed = rotateSpeed;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (_targetAnchor == null)
            Debug.LogError("FaceTargetAction has no target AnchorIDSO assigned.", stateMachine.Owner);

        _locomotion = new AnchorReference<BaseLocomotion>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    public override void OnStateEnter()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        _previousRotateSpeed = locomotion.GetRotateSmoothSpeed();
        locomotion.SetRotateSmoothSpeed(_rotateSpeed);
    }

    public override void OnUpdate()
    {
        if (_targetAnchor == null ||
            !_locomotion.TryGet(out BaseLocomotion locomotion) ||
            !_target.TryGet(out Transform target))
            return;

        Vector3 direction = target.position - locomotion.transform.position;
        locomotion.SetFacingDirection(direction);
    }

    public override void OnStateExit()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        locomotion.SetRotateSmoothSpeed(_previousRotateSpeed);
        locomotion.ClearFacingDirection();
    }

}
