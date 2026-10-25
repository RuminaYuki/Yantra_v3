using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(CharacterTeleporter))]
[RequiresOwnerComponent(typeof(StandablePointValidator))]
[CreateAssetMenu(
    fileName = "TeleportAnchorRing_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Teleport Anchor Ring")]
public class TeleportAnchorRingActionSO : StateActionSO
{
    [Tooltip("Teleports to a random free spot in a ring around this. Empty = Owner. Self Anchor is the one that teleports.")]
    [SerializeField] private AnchorIDSO _targetAnchor;
    [SerializeField] private bool _faceTarget = true;
    [SerializeField,Exposed] private RingSampleParameters _ringParameters =
        new() { radius = 8f, minDistance = 4f, directionCount = 16 };
    [SerializeField, Min(1)] private int _maxAttempts = 3;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new TeleportAnchorRingAction(
            _targetAnchor,
            _faceTarget,
            _ringParameters,
            _maxAttempts);
    }
}

// Failed: no valid spot after Max Attempts (or a needed component is missing), so it didn't move.
public class TeleportAnchorRingAction : StateAction, IFallibleAction
{
    public bool Failed { get; private set; }

    private readonly AnchorIDSO _targetAnchor;
    private readonly bool _faceTarget;
    private readonly RingSampleParameters _ringParameters;
    private readonly int _maxAttempts;

    private AnchorReference<CharacterTeleporter> _teleporter;
    private AnchorReference<StandablePointValidator> _pointValidator;
    private AnchorReference<Transform> _target;
    private GameObject _owner;

    public TeleportAnchorRingAction(
        AnchorIDSO targetAnchor,
        bool faceTarget,
        RingSampleParameters ringParameters,
        int maxAttempts)
    {
        _targetAnchor = targetAnchor;
        _faceTarget = faceTarget;
        _ringParameters = ringParameters;
        _maxAttempts = Mathf.Max(1, maxAttempts);
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
        // Cleared only when it teleports.
        Failed = true;

        if (!_teleporter.TryGet(out CharacterTeleporter teleporter) ||
            !_pointValidator.TryGet(out StandablePointValidator pointValidator) ||
            !_target.TryGet(out Transform target))
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

            Failed = false;
            return;
        }

        Debug.LogWarning(
            $"TeleportAnchorRingAction could not find a valid position after {_maxAttempts} attempts.",
            _owner);
    }

    public override void OnUpdate() { }
}
