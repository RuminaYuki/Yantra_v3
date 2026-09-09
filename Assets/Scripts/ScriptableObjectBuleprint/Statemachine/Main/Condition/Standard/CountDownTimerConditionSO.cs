using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewCountDownTimer_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/Count Down Timer")]
public class CountDownTimerConditionSO : StateConditionSO
{
    [Header("Set Max Duration to 0 to disable randomization")]
    [SerializeField] private float minDuration = 0f;
    [SerializeField] private float maxDuration = 0f;
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    public override Condition CreateCondition()
    {
        return new CountDownTimerCondition(minDuration, maxDuration, enableDebugLogs);
    }
}

public class CountDownTimerCondition : Condition
{
    private readonly float _minDuration;
    private readonly float _maxDuration;
    private readonly bool _enableDebugLogs;

    private float _duration;
    private float _elapsed;

    public CountDownTimerCondition(float minDuration, float maxDuration, bool enableDebugLogs)
    {
        _minDuration = minDuration;
        _maxDuration = maxDuration;
        _enableDebugLogs = enableDebugLogs;
    }

    public override void OnStateEnter()
    {
        _duration = _maxDuration > 0f
            ? Random.Range(_minDuration, _maxDuration)
            : _minDuration;

        _elapsed = 0f;
    }

    protected override bool Statement()
    {
        _elapsed += Time.deltaTime;

        if (_enableDebugLogs)
            Debug.Log($"CountDownTimerCondition: {Mathf.Max(_duration - _elapsed, 0f)} seconds remaining.");

        return _elapsed >= _duration;
    }
}
