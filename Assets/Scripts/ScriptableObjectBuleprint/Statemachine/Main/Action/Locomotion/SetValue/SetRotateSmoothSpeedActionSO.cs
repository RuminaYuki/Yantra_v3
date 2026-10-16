using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "SetTurnSmoothSpeedAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/SetBaseLocomotionValue/Set Turn Smooth Speed")]
public class SetRotateSmoothSpeedActionSO : StateActionSO
{
    [SerializeField, Min(0f)] private float _rotateSmoothSpeed = 1f;
    [SerializeField] private bool _resetOnStateExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
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
    private AnchorReference<BaseLocomotion> _locomotion;
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
        _locomotion = new AnchorReference<BaseLocomotion>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        _previousRotateSmoothSpeed =
            locomotion.GetRotateSmoothSpeed();
        locomotion.SetRotateSmoothSpeed(_rotateSmoothSpeed);
        _isApplied = true;
    }

    public override void OnStateExit()
    {
        if (!_isApplied || !_locomotion.TryGet(out BaseLocomotion locomotion))
            return;

        if (_resetOnStateExit)
        {
            locomotion.SetRotateSmoothSpeed(
                _previousRotateSmoothSpeed);
        }

        _isApplied = false;
    }

    public override void OnUpdate() { }
}
