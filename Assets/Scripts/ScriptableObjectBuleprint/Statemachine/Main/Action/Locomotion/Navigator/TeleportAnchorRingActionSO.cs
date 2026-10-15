using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(CharacterTeleporter))]
[CreateAssetMenu(
    fileName = "TeleportAnchorRing_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Teleport Anchor Ring")]
public class TeleportAnchorRingActionSO : StateActionSO
{
    [Tooltip("Where to teleport. Empty = Owner. Self Anchor is the one that teleports.")]
    [SerializeField] private AnchorIDSO _targetAnchor;
    [SerializeField] private bool _faceTarget = true;

    [Header("Random Around Target (needs StandablePointValidator)")]
    [SerializeField] private bool _randomAroundTarget = false;
    [SerializeField] private RingSampleParameters _ringParameters =
        new() { radius = 8f, minDistance = 4f, directionCount = 16 };
    [SerializeField, Min(1)] private int _maxAttempts = 3;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new TeleportAnchorRingAction(
            _targetAnchor,
            _faceTarget,
            _randomAroundTarget,
            _ringParameters,
            _maxAttempts);
    }
}

public class TeleportAnchorRingAction : StateAction
{
    private readonly AnchorIDSO _targetAnchor;
    private readonly bool _faceTarget;
    private readonly bool _randomAroundTarget;
    private readonly RingSampleParameters _ringParameters;
    private readonly int _maxAttempts;

    private AnchorReference<CharacterTeleporter> _teleporter;
    private AnchorReference<StandablePointValidator> _pointValidator;
    private AnchorReference<Transform> _target;
    private GameObject _owner;

    public TeleportAnchorRingAction(
        AnchorIDSO targetAnchor,
        bool faceTarget,
        bool randomAroundTarget,
        RingSampleParameters ringParameters,
        int maxAttempts)
    {
        _targetAnchor = targetAnchor;
        _faceTarget = faceTarget;
        _randomAroundTarget = randomAroundTarget;
        _ringParameters = ringParameters;
        _maxAttempts = Mathf.Max(1, maxAttempts);
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner;
        _teleporter = new AnchorReference<CharacterTeleporter>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);

        if (_randomAroundTarget)
            _pointValidator = new AnchorReference<StandablePointValidator>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_teleporter.TryGet(out CharacterTeleporter teleporter) ||
            !_target.TryGet(out Transform target))
            return;

        if (!_randomAroundTarget)
        {
            // Standing on the target: face the way the target faces instead of looking at itself.
            teleporter.Teleport(target.position);
            if (_faceTarget)
                teleporter.transform.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            return;
        }

        if (!_pointValidator.TryGet(out StandablePointValidator pointValidator))
            return;

        Vector3 origin = target.position;

        for (int i = 0; i < _maxAttempts; i++)
        {
            if (!RingPointSampler.TryGetPoint(
                    pointValidator,
                    origin,
                    in _ringParameters,
                    out Vector3 position))
                continue;

            if (_faceTarget)
                teleporter.TeleportAndFace(position, origin);
            else
                teleporter.Teleport(position);

            return;
        }

        Debug.LogWarning(
            $"TeleportAnchorRingAction could not find a valid position after {_maxAttempts} attempts.",
            _owner);
    }

    public override void OnUpdate() { }
}
