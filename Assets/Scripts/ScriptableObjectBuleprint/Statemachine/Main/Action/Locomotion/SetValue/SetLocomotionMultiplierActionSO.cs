using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetLocomotionMultiplier_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Locomotion/SetBaseLocomotionValue/Set Locomotion Multiplier")]
public class SetLocomotionMultiplierActionSO : StateActionSO
{
    [SerializeField] private float _multiplier = 1f;
    [SerializeField] private bool _resetOnStateExit = true;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetLocomotionMultiplierAction(
            _multiplier,
            _resetOnStateExit);
    }
}

public class SetLocomotionMultiplierAction : StateAction
{
    private readonly float _multiplier;
    private readonly bool _resetOnStateExit;
    private BaseLocomotion _locomotion;
    private float _previousMultiplier;
    private bool _isApplied;

    public SetLocomotionMultiplierAction(
        float multiplier,
        bool resetOnStateExit)
    {
        _multiplier = multiplier;
        _resetOnStateExit = resetOnStateExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _locomotion = stateMachine.GetComponent<BaseLocomotion>();

        if (_locomotion == null)
            Debug.LogError("SetLocomotionMultiplierAction cannot find BaseLocomotion.");
    }

    public override void OnStateEnter()
    {
        if (_locomotion == null)
            return;

        _previousMultiplier = _locomotion.GetMoveMultiply();
        _locomotion.SetMoveMultiply(_multiplier);
        _isApplied = true;
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (_locomotion == null || !_isApplied)
            return;

        // Another concurrent state machine (e.g. a combat state) may have
        // overwritten the multiplier since OnStateEnter. Only restore our
        // previous value if it's still the one we set, so we don't clobber
        // a value some other action applied in the meantime.
        if (_resetOnStateExit && Mathf.Approximately(_locomotion.GetMoveMultiply(), _multiplier))
            _locomotion.SetMoveMultiply(_previousMultiplier);

        _isApplied = false;
    }
}
