using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "CountDownTimer_Action", 
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Standard/CountDownTimerAction")]
public class CountDownTimerActionSO : StateActionSO
{
    [Header("Set Max Duration to 0 to disable randomization")]
    public float MinDuration = 0f;
    public float MaxDuration = 0f;
    [Tooltip("The flag to set when the timer completes")]
    public FlagSO FlagSO;
    [Header("Debug")]
    public bool EnableDebugLogs;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new CountDownTimerAction(MinDuration, MaxDuration, FlagSO, EnableDebugLogs);
    }
}

public class CountDownTimerAction : StateAction
{
    private float _minDuration;
    private float _maxDuration;
    private FlagSO _flagSO;

    private float _currentTime;
    private StateFlags _stateFlags;
    private bool _enableDebugLogs = false;
    

    public CountDownTimerAction(float minDuration, float maxDuration, FlagSO flagSO, bool enableDebugLogs)
    {
        _minDuration = minDuration;
        _maxDuration = maxDuration;
        _flagSO = flagSO;
        _enableDebugLogs = enableDebugLogs;
    }
    public override void Awake(StateMachine stateMachine)
    {
        _stateFlags = stateMachine.GetComponent<StateFlags>();
        if (_stateFlags == null)
            Debug.LogError("CountDownTimerAction requires a StateFlags component on the same GameObject.");
    }

    public override void OnStateEnter()
    {
        float duration = _maxDuration > 0f
        ? Random.Range(_minDuration, _maxDuration) : _minDuration;

        _currentTime = duration;
        _stateFlags.Set(_flagSO, false);
    }
    public override void OnUpdate()
    {
        if (_enableDebugLogs)
            Debug.Log($"CountDownTimerAction: {_currentTime} seconds remaining.");

        _currentTime -= Time.deltaTime;

        if (_currentTime <= 0f)
            _stateFlags.Set(_flagSO, true);
    }
    public override void OnStateExit()
    {
        _stateFlags.Set(_flagSO, false);
    }
}
