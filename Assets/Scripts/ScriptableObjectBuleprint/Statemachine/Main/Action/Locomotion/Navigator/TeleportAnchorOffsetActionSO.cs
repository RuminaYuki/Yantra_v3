using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(CharacterTeleporter))]
[RequiresOwnerComponent(typeof(StandablePointValidator))]
[CreateAssetMenu(
    fileName = "TeleportAnchorOffset_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Teleport Anchor Offset")]
public class TeleportAnchorOffsetActionSO : StateActionSO
{
    [Tooltip("Offset is relative to this. Empty = Owner. Self Anchor is the one that teleports.")]
    [SerializeField] private AnchorIDSO _targetAnchor;

    [Tooltip("Local to target's facing (yaw only). Z+ = in front, Z- = behind. Min = Max for a fixed offset.")]
    [SerializeField] private Vector3 _offsetMin = new(-2f, 0f, -4f);
    [SerializeField] private Vector3 _offsetMax = new(2f, 0f, -2f);
    [SerializeField, Min(1)] private int _maxAttempts = 8;
    [SerializeField] private bool _faceTarget = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new TeleportAnchorOffsetAction(
            _targetAnchor,
            _offsetMin,
            _offsetMax,
            _maxAttempts,
            _faceTarget);
    }
}

public class TeleportAnchorOffsetAction : StateAction
{
    private readonly AnchorIDSO _targetAnchor;
    private readonly Vector3 _offsetMin;
    private readonly Vector3 _offsetMax;
    private readonly int _maxAttempts;
    private readonly bool _faceTarget;

    private AnchorReference<CharacterTeleporter> _teleporter;
    private AnchorReference<StandablePointValidator> _pointValidator;
    private AnchorReference<Transform> _target;
    private GameObject _owner;

    public TeleportAnchorOffsetAction(
        AnchorIDSO targetAnchor,
        Vector3 offsetMin,
        Vector3 offsetMax,
        int maxAttempts,
        bool faceTarget)
    {
        _targetAnchor = targetAnchor;
        _offsetMin = Vector3.Min(offsetMin, offsetMax);
        _offsetMax = Vector3.Max(offsetMin, offsetMax);
        _maxAttempts = Mathf.Max(1, maxAttempts);
        _faceTarget = faceTarget;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner;
        _teleporter = new AnchorReference<CharacterTeleporter>(stateMachine, this);
        _pointValidator = new AnchorReference<StandablePointValidator>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    public override void OnStateEnter()
    {
        if (!_teleporter.TryGet(out CharacterTeleporter teleporter) ||
            !_pointValidator.TryGet(out StandablePointValidator pointValidator) ||
            !_target.TryGet(out Transform target))
            return;

        Vector3 origin = target.position;
        Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);

        for (int i = 0; i < _maxAttempts; i++)
        {
            Vector3 localOffset = new(
                Random.Range(_offsetMin.x, _offsetMax.x),
                Random.Range(_offsetMin.y, _offsetMax.y),
                Random.Range(_offsetMin.z, _offsetMax.z));

            Vector3 candidate = origin + yaw * localOffset;

            if (!pointValidator.TryValidatePoint(origin, candidate, out Vector3 position))
                continue;

            if (_faceTarget)
                teleporter.TeleportAndFace(position, origin);
            else
                teleporter.Teleport(position);

            return;
        }

        Debug.LogWarning(
            $"TeleportAnchorOffsetAction could not find a valid position after {_maxAttempts} attempts.",
            _owner);
    }

    public override void OnUpdate() { }
}
