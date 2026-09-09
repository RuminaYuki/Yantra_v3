using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetGravityMultiplierAction",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Locomotion/SetBaseLocomotionValue/Set Gravity Multiplier")]
public class SetGravityMultiplierActionSO : StateActionSO
{
    [SerializeField, Min(0f)] private float _multiplier = 1f;
    [SerializeField] private bool _resetOnStateExit = true;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetGravityMultiplierAction(
            _multiplier,
            _resetOnStateExit);
    }
}

public class SetGravityMultiplierAction : StateAction
{
    private readonly float _multiplier;
    private readonly bool _resetOnStateExit;
    private BaseLocomotion _locomotion;
    private float _previousMultiplier;
    private bool _isApplied;

    public SetGravityMultiplierAction(
        float multiplier,
        bool resetOnStateExit)
    {
        _multiplier = Mathf.Max(0f, multiplier);
        _resetOnStateExit = resetOnStateExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _locomotion = stateMachine.GetComponent<BaseLocomotion>();

        if (_locomotion == null)
            Debug.LogError(
                "SetGravityMultiplierAction requires BaseLocomotion.",
                stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        if (_locomotion == null)
            return;

        _previousMultiplier = _locomotion.GetGravityMultiplier();
        _locomotion.SetGravityMultiplier(_multiplier);
        _isApplied = true;
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (_locomotion == null || !_isApplied)
            return;

        if (_resetOnStateExit)
            _locomotion.SetGravityMultiplier(_previousMultiplier);

        _isApplied = false;
    }
}
