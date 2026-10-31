using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "Dash_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Dash")]
public class DashActionSO : StateActionSO
{
    [Tooltip("Dash toward this (direction only; it always goes the full Distance). Empty = straight ahead. Self Anchor is the one that dashes.")]
    [SerializeField] private AnchorIDSO _targetAnchor;
    [SerializeField,Exposed] private TargetLeadSettings _lead =
        new() { usePrediction = false, leadTime = 0.3f, maxDistance = 3f };

    [Tooltip("How far the dash goes.")]
    [SerializeField, Min(0f),Exposed] private float _distance = 6f;
    [SerializeField, Min(0.01f),Exposed] private float _duration = 0.3f;
    [Tooltip("Speed over the dash (0..1 time). Its average is scaled so the dash still covers Distance.")]
    [SerializeField] private AnimationCurve _speedCurve = AnimationCurve.Constant(0f, 1f, 1f);
    [SerializeField] private bool _faceDirection = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new DashAction(
            _targetAnchor,
            _lead,
            _distance,
            _duration,
            _speedCurve,
            _faceDirection);
    }
}

// Moves through BaseLocomotion (Velocity mode), so gravity and the CharacterController stay in one place.
// Failed: something blocked the dash (hit a wall) or a needed component is missing.
public class DashAction : StateAction, IFallibleAction
{
    public bool Failed { get; private set; }

    // Moved less than this part of what it should have = blocked.
    private const float BlockedRatio = 0.2f;

    private readonly AnchorIDSO _targetAnchor;
    private readonly TargetLeadSettings _lead;
    private readonly float _distance;
    private readonly float _duration;
    private readonly AnimationCurve _speedCurve;
    private readonly bool _faceDirection;
    private readonly float _curveAverage;

    private AnchorReference<BaseLocomotion> _locomotion;
    private AnchorReference<Transform> _target;

    private LocomotionMoveMode _previousMoveMode;
    private float _previousSpeed;
    private bool _isApplied;
    private bool _isDashing;

    private Vector3 _direction;
    private float _elapsed;
    private Vector3 _lastPosition;
    private float _lastSpeed;

    public DashAction(
        AnchorIDSO targetAnchor,
        TargetLeadSettings lead,
        float distance,
        float duration,
        AnimationCurve speedCurve,
        bool faceDirection)
    {
        _targetAnchor = targetAnchor;
        _lead = lead;
        _distance = Mathf.Max(0f, distance);
        _duration = Mathf.Max(0.01f, duration);
        _speedCurve = speedCurve;
        _faceDirection = faceDirection;
        _curveAverage = AverageOf(speedCurve);
    }

    public override void Awake(StateMachine stateMachine)
    {
        _locomotion = new AnchorReference<BaseLocomotion>(stateMachine, this);
        if (_targetAnchor != null)
            _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    public override void OnStateEnter()
    {
        Failed = true;
        _isDashing = false;
        _elapsed = 0f;
        _lastSpeed = 0f;

        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;

        Transform self = locomotion.transform;
        _direction = self.forward;

        if (_target != null && _target.TryGet(out Transform target))
        {
            Vector3 toTarget = TargetLead.Predict(target, in _lead) - self.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
                _direction = toTarget;
        }

        _direction.y = 0f;
        _direction.Normalize();

        _previousMoveMode = locomotion.MoveMode;
        _previousSpeed = locomotion.CurrentSpeed;
        locomotion.MoveMode = LocomotionMoveMode.Velocity;
        _isApplied = true;

        if (_faceDirection)
            locomotion.SetFacingDirection(_direction);

        Failed = false;
        _isDashing = _distance > 0f;
        _lastPosition = self.position;
        if (!_isDashing)
            Stop(locomotion);
    }

    public override void OnUpdate()
    {
        if (!_isDashing || !_locomotion.TryGet(out BaseLocomotion locomotion)) return;

        Vector3 position = locomotion.transform.position;
        if (IsBlocked(position))
        {
            Failed = true;
            Stop(locomotion);
            return;
        }
        _lastPosition = position;

        _elapsed += Time.DeltaTime;
        if (_elapsed >= _duration)
        {
            Stop(locomotion);
            return;
        }

        float t = _elapsed / _duration;
        float speed = _distance / _duration * Mathf.Max(0f, _speedCurve.Evaluate(t)) / _curveAverage;
        locomotion.CurrentSpeed = speed;
        locomotion.SetMovementDirection(_direction);
        _lastSpeed = speed;
    }

    public override void OnStateExit()
    {
        if (!_isApplied || !_locomotion.TryGet(out BaseLocomotion locomotion))
            return;

        locomotion.ClearMovementDirection();
        if (_faceDirection)
            locomotion.ClearFacingDirection();
        locomotion.CurrentSpeed = _previousSpeed;
        locomotion.MoveMode = _previousMoveMode;

        _isApplied = false;
        _isDashing = false;
    }

    private void Stop(BaseLocomotion locomotion)
    {
        _isDashing = false;
        locomotion.ClearMovementDirection();
        locomotion.CurrentSpeed = 0f;
    }

    // Since the last frame it should have moved about lastSpeed * deltaTime (sideways only).
    // Unity time, because that is what BaseLocomotion moves with.
    private bool IsBlocked(Vector3 position)
    {
        float expected = _lastSpeed * UnityEngine.Time.deltaTime;
        if (expected < 0.01f)
            return false;

        Vector3 moved = position - _lastPosition;
        moved.y = 0f;
        return moved.magnitude < expected * BlockedRatio;
    }

    // So any curve shape still covers the whole distance in the duration.
    private static float AverageOf(AnimationCurve curve)
    {
        if (curve == null || curve.length == 0)
            return 1f;

        const int samples = 32;
        float sum = 0f;
        for (int i = 0; i < samples; i++)
            sum += Mathf.Max(0f, curve.Evaluate((i + 0.5f) / samples));

        return sum > 0.0001f ? sum / samples : 1f;
    }
}
