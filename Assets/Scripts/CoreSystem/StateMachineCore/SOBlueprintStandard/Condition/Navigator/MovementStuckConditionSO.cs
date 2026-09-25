using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "MovementStuck_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Navigator/Movement Stuck")]
//ยังไม่พร้อมให้ใช้งาน
public class MovementStuckConditionSO : StateConditionSO
{
    [Tooltip("Speed (units/sec) below which the owner is considered not moving.")]
    [SerializeField, Min(0f)] private float _minMoveSpeed = 0.1f;
    [Tooltip("How long the owner must stay below MinMoveSpeed before this condition becomes true.")]
    [SerializeField, Min(0f)] private float _stuckDuration = 1f;

    public float MinMoveSpeed
    {
        get => _minMoveSpeed;
        set => _minMoveSpeed = Mathf.Max(0f, value);
    }
    public float StuckDuration
    {
        get => _stuckDuration;
        set => _stuckDuration = Mathf.Max(0f, value);
    }

    public override Condition CreateCondition()
    {
        return new MovementStuckCondition(_minMoveSpeed, _stuckDuration);
    }
}

public class MovementStuckCondition : Condition
{
    private readonly float _minMoveSpeed;
    private readonly float _stuckDuration;

    private Transform _owner;
    private Vector3 _windowStartPosition;
    private float _windowElapsed;

    public float StuckTimer { get; private set; }

    public MovementStuckCondition(float minMoveSpeed, float stuckDuration)
    {
        _minMoveSpeed = Mathf.Max(0f, minMoveSpeed);
        _stuckDuration = Mathf.Max(0f, stuckDuration);
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner.transform;
        _windowStartPosition = _owner.position;
    }

    public override void OnStateEnter()
    {
        if (_owner != null)
        {
            _windowStartPosition = _owner.position;
        }
        _windowElapsed = 0f;
        StuckTimer = 0f;
    }

    protected override bool Statement()
    {
        if (_owner == null)
        {
            return false;
        }

        _windowElapsed += Time.deltaTime;

        Vector3 totalDelta = _owner.position - _windowStartPosition;
        totalDelta.y = 0f;

        float averageSpeed = totalDelta.magnitude / Mathf.Max(_windowElapsed, 0.0001f);

        if (averageSpeed < _minMoveSpeed)
        {
            StuckTimer = _windowElapsed;
        }
        else
        {
            // Moved too far this window - restart the window from here instead of the raw per-frame speed,
            // so a single jittery frame (collision push, rotation offset) doesn't reset progress to 0.
            _windowStartPosition = _owner.position;
            _windowElapsed = 0f;
            StuckTimer = 0f;
        }

        return StuckTimer >= _stuckDuration;
    }
}
