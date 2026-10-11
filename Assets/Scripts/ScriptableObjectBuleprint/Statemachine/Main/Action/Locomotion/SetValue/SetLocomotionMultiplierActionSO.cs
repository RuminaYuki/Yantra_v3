using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "SetLocomotionMultiplier_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/SetBaseLocomotionValue/Set Locomotion Multiplier")]
public class SetLocomotionMultiplierActionSO : StateActionSO
{
    [SerializeField] private float _multiplier = 1f;
    [SerializeField] private bool _resetOnStateExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
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
    private AnchorReference<BaseLocomotion> _locomotion;
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
        _locomotion = new AnchorReference<BaseLocomotion>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        _previousMultiplier = locomotion.GetMoveMultiply();
        locomotion.SetMoveMultiply(_multiplier);
        _isApplied = true;
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (!_isApplied || !_locomotion.TryGet(out BaseLocomotion locomotion))
            return;

        // Another concurrent state machine (e.g. a combat state) may have
        // overwritten the multiplier since OnStateEnter. Only restore our
        // previous value if it's still the one we set, so we don't clobber
        // a value some other action applied in the meantime.
        if (_resetOnStateExit && Mathf.Approximately(locomotion.GetMoveMultiply(), _multiplier))
            locomotion.SetMoveMultiply(_previousMultiplier);

        _isApplied = false;
    }
}
