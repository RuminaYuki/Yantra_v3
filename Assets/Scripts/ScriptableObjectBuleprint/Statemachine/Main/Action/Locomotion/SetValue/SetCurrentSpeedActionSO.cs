using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetSpeedAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/SetBaseLocomotionValue/SetCurrentSpeed")]
public class SetCurrentSpeedActionSO : StateActionSO
{
    [SerializeField, Min(0f)] private float _speed = 5f;
    [SerializeField] private bool _resetOnStateExit = true;
    public float Speed
    {
        get => _speed;
        set
        {
            if(value < 0)
            {
                Debug.LogWarning("speed value cant below zero");
                return;
            }
           _speed = value;
        }
    }

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetCurrentSpeedAction(
            _speed,
            _resetOnStateExit);
    }
}

public class SetCurrentSpeedAction : StateAction
{
    private readonly float _speed;
    private readonly bool _resetOnStateExit;
    private BaseLocomotion _locomotion;
    private float _previousSpeed;
    private bool _isApplied;

    public SetCurrentSpeedAction(
        float speed,
        bool resetOnStateExit)
    {
        _speed = Mathf.Max(0f, speed);
        _resetOnStateExit = resetOnStateExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _locomotion = stateMachine.GetComponent<BaseLocomotion>();

        if (_locomotion == null)
            Debug.LogError("SetCurrentSpeedAction cannot find BaseLocomotion.");
    }

    public override void OnStateEnter()
    {
        if (_locomotion == null)
            return;

        _previousSpeed = _locomotion.CurrentSpeed;
        _locomotion.CurrentSpeed = _speed;
        _isApplied = true;
    }

    public override void OnStateExit()
    {
        if (_locomotion == null || !_isApplied)
            return;

        if (_resetOnStateExit)
            _locomotion.CurrentSpeed = _previousSpeed;

        _isApplied = false;
    }

    public override void OnUpdate() { }
}
