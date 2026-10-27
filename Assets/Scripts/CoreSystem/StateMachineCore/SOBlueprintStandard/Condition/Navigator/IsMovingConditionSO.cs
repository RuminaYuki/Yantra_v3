using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "IsMoving_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Navigator/Is Moving")]
public class IsMovingConditionSO : StateConditionSO
{
    [Tooltip("Speed (units/sec) at or above which the owner is considered moving.")]
    [SerializeField, Min(0f)] private float _minMoveSpeed = 0.1f;
    [Tooltip("Ignore vertical movement (jump/fall/slope) when measuring speed.")]
    [SerializeField] private bool _ignoreY = true;
    [Tooltip("Higher = reacts faster, lower = smoother against single-frame jitter. 0 = no smoothing.")]
    [SerializeField, Min(0f)] private float _smoothing = 15f;

    protected override Condition OnCreateCondition()
    {
        return new IsMovingCondition(_minMoveSpeed, _ignoreY, _smoothing);
    }
}

public class IsMovingCondition : Condition
{
    private readonly float _minMoveSpeed;
    private readonly bool _ignoreY;
    private readonly float _smoothing;

    private Transform _owner;
    private Vector3 _lastPosition;

    public float CurrentSpeed { get; private set; }

    public IsMovingCondition(float minMoveSpeed, bool ignoreY, float smoothing)
    {
        _minMoveSpeed = Mathf.Max(0f, minMoveSpeed);
        _ignoreY = ignoreY;
        _smoothing = Mathf.Max(0f, smoothing);
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner.transform;
        _lastPosition = _owner.position;
    }

    public override void OnStateEnter()
    {
        if (_owner != null)
        {
            _lastPosition = _owner.position;
        }
        CurrentSpeed = 0f;
    }

    protected override void OnTick()
    {
        // ใช้ delta ของ state machine: ตัวที่ถูก slow ยังนับว่า "กำลังเดิน" ด้วย threshold เดิม
        float dt = Time.DeltaTime;
        if (_owner == null || dt <= 0f)
        {
            return;
        }

        Vector3 delta = _owner.position - _lastPosition;
        _lastPosition = _owner.position;
        if (_ignoreY)
        {
            delta.y = 0f;
        }

        float rawSpeed = delta.magnitude / dt;

        CurrentSpeed = _smoothing <= 0f
            ? rawSpeed
            : Mathf.Lerp(CurrentSpeed, rawSpeed, 1f - Mathf.Exp(-_smoothing * dt));
    }

    protected override bool Statement()
    {
        if (_owner == null)
        {
            return false;
        }
        return CurrentSpeed >= _minMoveSpeed;
    }
}
