using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewCDTimer_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/Count Down Timer")]
public class CountDownTimerConditionSO : StateConditionSO
{
    [Header("Set Max Duration to 0 to disable randomization")]
    [SerializeField] private float minDuration = 0f;
    [SerializeField] private float maxDuration = 0f;

    #region Properties
    public float MinDuration
    {
        get => minDuration;
        set
        {
            if (value < 0f)
            {
                Debug.LogWarning("Min duration cannot be negative. Setting to 0.");
                minDuration = 0f;
                return;
            }
            minDuration = value;
        }
    }

    public float MaxDuration
    {
        get => maxDuration;
        set
        {
            if (value < 0f)
            {
                Debug.LogWarning("Max duration cannot be negative. Setting to 0.");
                maxDuration = 0f;
                return;
            }
            maxDuration = value;
        }
    }
    #endregion

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
