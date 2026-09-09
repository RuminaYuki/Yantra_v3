using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetTurnSmoothSpeedAction",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Locomotion/SetBaseLocomotionValue/Set Turn Smooth Speed")]
public class SetRotateSmoothSpeedActionSO : StateActionSO
{
    [SerializeField, Min(0f)] private float _rotateSmoothSpeed = 1f;
    [SerializeField] private bool _resetOnStateExit = true;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetRotateSmoothSpeedAction(
            _rotateSmoothSpeed,
            _resetOnStateExit);
    }
}

public class SetRotateSmoothSpeedAction : StateAction
{
    private readonly float _rotateSmoothSpeed;
    private readonly bool _resetOnStateExit;
    private BaseLocomotion _locomotion;
    private float _previousRotateSmoothSpeed;
    private bool _isApplied;

    public SetRotateSmoothSpeedAction(
        float rotateSmoothSpeed,
        bool resetOnStateExit)
    {
        _rotateSmoothSpeed = Mathf.Max(0f, rotateSmoothSpeed);
        _resetOnStateExit = resetOnStateExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _locomotion = stateMachine.GetComponent<BaseLocomotion>();

        if (_locomotion == null)
            Debug.LogError("SetRotateSmoothSpeedAction cannot find BaseLocomotion.");
    }

    public override void OnStateEnter()
    {
        if (_locomotion == null)
            return;

        _previousRotateSmoothSpeed =
            _locomotion.GetRotateSmoothSpeed();
        _locomotion.SetRotateSmoothSpeed(_rotateSmoothSpeed);
        _isApplied = true;
    }

    public override void OnStateExit()
    {
        if (_locomotion == null || !_isApplied)
            return;

        if (_resetOnStateExit)
        {
            _locomotion.SetRotateSmoothSpeed(
                _previousRotateSmoothSpeed);
        }

        _isApplied = false;
    }

    public override void OnUpdate() { }
}
