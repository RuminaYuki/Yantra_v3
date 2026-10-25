using UnityEngine;
using UnityEngine.Serialization;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Starts a cooldown when the state is entered (or left). It keeps counting after the state ends;
// check it with Cooldown Ready (CooldownReadyConditionSO).
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(CooldownController))]
[CreateAssetMenu(
    fileName = "NewStartCooldown_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Start Cooldown")]
public class StartCooldownActionSO : StateActionSO
{
    public enum When
    {
        OnEnter,
        OnExit
    }

    [SerializeField] private CooldownSO _cooldown;
    [SerializeField, Min(0f), Exposed, FormerlySerializedAs("_duration")] private float _minDuration = 1f;
    [Tooltip("Above Min Duration: each start picks a random time between the two. 0 = always Min Duration.")]
    [SerializeField, Min(0f), Exposed] private float _maxDuration;
    [SerializeField] private When _when = When.OnEnter;
    [Tooltip("Restart: always from the new duration. Keep Longer: only if it would end later than what's left.")]
    [SerializeField] private CooldownController.StartMode _mode = CooldownController.StartMode.Restart;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new StartCooldownAction(_cooldown, _minDuration, _maxDuration, _when, _mode);
    }
}

public class StartCooldownAction : StateAction
{
    private readonly CooldownSO _cooldown;
    private readonly float _minDuration;
    private readonly float _maxDuration;
    private readonly StartCooldownActionSO.When _when;
    private readonly CooldownController.StartMode _mode;
    private AnchorReference<CooldownController> _controller;

    public StartCooldownAction(
        CooldownSO cooldown,
        float minDuration,
        float maxDuration,
        StartCooldownActionSO.When when,
        CooldownController.StartMode mode)
    {
        _cooldown = cooldown;
        _minDuration = minDuration;
        _maxDuration = maxDuration;
        _when = when;
        _mode = mode;
    }

    public override void Awake(StateMachine stateMachine)
    {
        // Self Anchor empty = this character's cooldowns; set = another object's.
        _controller = new AnchorReference<CooldownController>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (_when == StartCooldownActionSO.When.OnEnter)
        {
            StartCooldown();
        }
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (_when == StartCooldownActionSO.When.OnExit)
        {
            StartCooldown();
        }
    }

    private void StartCooldown()
    {
        if (_cooldown != null && _controller.TryGet(out CooldownController controller))
        {
            // Rolled again on every start.
            float duration = _maxDuration > _minDuration ? Random.Range(_minDuration, _maxDuration) : _minDuration;
            controller.StartCooldown(_cooldown, duration, _mode);
        }
    }
}
