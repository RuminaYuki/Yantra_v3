using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Moves an object to a target's position once, when the state starts, by setting Transform.position.
// For anything without the locomotion setup (props, IK targets, points ...).
// Self Anchor = the object that moves (empty = this state machine's owner).
// A CharacterController on it is turned off for the move, otherwise it would put the object back.
[UseSelfAnchor][MenuGroup("Standard")]
[CreateAssetMenu(
    fileName = "NewTeleportTransform_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Teleport Transform")]
public class TeleportTransformActionSO : StateActionSO
{
    [Tooltip("Where to go. Empty = this state machine's owner.")]
    [SerializeField] private AnchorIDSO _targetAnchor;

    [Tooltip("Added to the target's position, in the target's local space (follows its rotation).")]
    [SerializeField, Exposed] private Vector3 _positionOffset;

    [Tooltip("Also copy the target's rotation.")]
    [SerializeField] private bool _matchRotation;
    [Tooltip("Only with Match Rotation: added on top of the target's rotation.")]
    [SerializeField, Exposed] private Vector3 _rotationOffset;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new TeleportTransformAction(_targetAnchor, _positionOffset, _matchRotation, _rotationOffset);
    }
}

public class TeleportTransformAction : StateAction
{
    private readonly AnchorIDSO _targetAnchor;
    private readonly Vector3 _positionOffset;
    private readonly bool _matchRotation;
    private readonly Vector3 _rotationOffset;

    private AnchorReference<Transform> _self;
    private AnchorReference<Transform> _target;

    public TeleportTransformAction(
        AnchorIDSO targetAnchor,
        Vector3 positionOffset,
        bool matchRotation,
        Vector3 rotationOffset)
    {
        _targetAnchor = targetAnchor;
        _positionOffset = positionOffset;
        _matchRotation = matchRotation;
        _rotationOffset = rotationOffset;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _self = new AnchorReference<Transform>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    public override void OnStateEnter()
    {
        if (!_self.TryGet(out Transform self) || !_target.TryGet(out Transform target)) return;

        // A CharacterController keeps its own position and would undo the move.
        CharacterController controller = self.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;

        // Rotation only, not TransformPoint: a scaled target shouldn't stretch the offset.
        self.position = target.position + target.rotation * _positionOffset;
        if (_matchRotation)
        {
            self.rotation = target.rotation * Quaternion.Euler(_rotationOffset);
        }

        if (wasEnabled) controller.enabled = true;
    }

    public override void OnUpdate() { }
}
