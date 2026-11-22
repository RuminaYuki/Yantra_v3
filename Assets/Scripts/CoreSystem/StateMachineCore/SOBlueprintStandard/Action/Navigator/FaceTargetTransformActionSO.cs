using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Like Face Target, but turns the Transform itself: no BaseLocomotion needed.
// For things without a locomotion (a turret, a head, a prop). Don't use it on an object that
// something else also rotates (BaseLocomotion, NavMeshAgent.updateRotation, root motion): they fight.
[UseSelfAnchor][MenuGroup("Standard")]
[CreateAssetMenu(
    fileName = "NewFaceTargetTransform_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Navigator/Face Target (Transform)")]
public class FaceTargetTransformActionSO : StateActionSO
{
    [Tooltip("Required. Self Anchor is the one that turns (empty = Owner).")]
    [SerializeField] private AnchorIDSO _targetAnchor;

    [Tooltip("How fast it turns, in degrees per second. 0 or less = snaps to face the target at once.")]
    [SerializeField] private float _degreesPerSecond = 360f;

    [Tooltip("On: turns left / right only (stays upright). Off: also tilts up / down to look straight at it.")]
    [SerializeField] private bool _yawOnly = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new FaceTargetTransformAction(_targetAnchor, _degreesPerSecond, _yawOnly);
    }
}

public class FaceTargetTransformAction : StateAction
{
    private readonly AnchorIDSO _targetAnchor;
    private readonly float _degreesPerSecond;
    private readonly bool _yawOnly;

    private AnchorReference<Transform> _self;
    private AnchorReference<Transform> _target;

    public FaceTargetTransformAction(AnchorIDSO targetAnchor, float degreesPerSecond, bool yawOnly)
    {
        _targetAnchor = targetAnchor;
        _degreesPerSecond = degreesPerSecond;
        _yawOnly = yawOnly;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (_targetAnchor == null)
            Debug.LogError("FaceTargetTransformAction has no target AnchorIDSO assigned.", stateMachine.Owner);

        _self = new AnchorReference<Transform>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    public override void OnUpdate()
    {
        if (_targetAnchor == null ||
            !_self.TryGet(out Transform self) ||
            !_target.TryGet(out Transform target))
            return;

        Vector3 direction = target.position - self.position;
        if (_yawOnly)
            direction.y = 0f;

        // On top of each other: no direction to face.
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
        self.rotation = _degreesPerSecond > 0f
            // Time = this state machine's clock (StateAction.Time): a slowed character turns slower too.
            ? Quaternion.RotateTowards(self.rotation, look, _degreesPerSecond * Time.DeltaTime)
            : look;
    }
}
