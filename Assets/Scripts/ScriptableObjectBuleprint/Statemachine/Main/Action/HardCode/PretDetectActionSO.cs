using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Drives Pret's detect meter (PretDetectComponent) from a state.
//   Enter: SetDetecting(Detecting On Enter), optionally empty the meter / change the rate.
//   Exit:  optionally SetDetecting(false) so the meter goes back down.
[RequiresOwnerComponent(typeof(PretDetectComponent))]
[CreateAssetMenu(
    fileName = "PretDetectAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/HardCode/Pret Detect")]
public class PretDetectActionSO : StateActionSO
{
    [Tooltip("true = meter goes up while in this state. false = goes down.")]
    [SerializeField] private bool _detectingOnEnter = true;

    [Tooltip("SetDetecting(false) when leaving this state.")]
    [SerializeField] private bool _stopDetectingOnExit = true;

    [Tooltip("SetDetectPoint(0) when entering this state.")]
    [SerializeField] private bool _emptyOnEnter;

    [Tooltip("Change the up/down speed when entering this state.")]
    [SerializeField] private bool _overrideRate;
    [SerializeField] [Min(0f)] private float _ratePerSecond = 10f;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new PretDetectAction(_detectingOnEnter, _stopDetectingOnExit, _emptyOnEnter, _overrideRate, _ratePerSecond);
    }
}

public class PretDetectAction : StateAction
{
    private readonly bool _detectingOnEnter;
    private readonly bool _stopDetectingOnExit;
    private readonly bool _emptyOnEnter;
    private readonly bool _overrideRate;
    private readonly float _ratePerSecond;

    private AnchorReference<PretDetectComponent> _detect;

    public PretDetectAction(bool detectingOnEnter, bool stopDetectingOnExit, bool emptyOnEnter, bool overrideRate, float ratePerSecond)
    {
        _detectingOnEnter = detectingOnEnter;
        _stopDetectingOnExit = stopDetectingOnExit;
        _emptyOnEnter = emptyOnEnter;
        _overrideRate = overrideRate;
        _ratePerSecond = ratePerSecond;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _detect = new AnchorReference<PretDetectComponent>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_detect.TryGet(out PretDetectComponent detect)) return;

        if (_overrideRate) detect.SetRatePerSecond(_ratePerSecond);
        if (_emptyOnEnter) detect.SetDetectPoint(0f);
        detect.SetDetecting(_detectingOnEnter);
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (!_stopDetectingOnExit) return;
        if (!_detect.TryGet(out PretDetectComponent detect)) return;

        detect.SetDetecting(false);
    }
}
