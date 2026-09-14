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

    public float Duration => _condition.Duration;
    public float Remaining => _condition.Remaining;

    private CountDownTimerCondition _condition;
    public override Condition CreateCondition()
    {
        _condition = new CountDownTimerCondition(minDuration, maxDuration);
        return _condition;
    }
}

public class CountDownTimerCondition : Condition
{
    private readonly float _minDuration;
    private readonly float _maxDuration;

    
    private float _elapsed;
    public float Duration { get; private set; }
    public float Remaining => Mathf.Max(0f, Duration - _elapsed);

    public CountDownTimerCondition(float minDuration, float maxDuration)
    {
        _minDuration = minDuration;
        _maxDuration = maxDuration;
    }

    public override void OnStateEnter()
    {
        Duration = _maxDuration > 0f
            ? Random.Range(_minDuration, _maxDuration)
            : _minDuration;

        _elapsed = 0f;
    }

    protected override bool Statement()
    {
        _elapsed += Time.deltaTime;

        return _elapsed >= Duration;
    }
}
