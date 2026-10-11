using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "SetGravityMultiplierAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/SetBaseLocomotionValue/Set Gravity Multiplier")]
public class SetGravityMultiplierActionSO : StateActionSO
{
    [SerializeField, Min(0f)] private float _multiplier = 1f;
    [SerializeField] private bool _resetOnStateExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
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
    private AnchorReference<BaseLocomotion> _locomotion;
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
        _locomotion = new AnchorReference<BaseLocomotion>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        _previousMultiplier = locomotion.GetGravityMultiplier();
        locomotion.SetGravityMultiplier(_multiplier);
        _isApplied = true;
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (!_isApplied || !_locomotion.TryGet(out BaseLocomotion locomotion))
            return;

        if (_resetOnStateExit)
            locomotion.SetGravityMultiplier(_previousMultiplier);

        _isApplied = false;
    }
}
