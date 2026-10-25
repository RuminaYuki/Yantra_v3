using UnityEngine;
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
    [SerializeField, Min(0f), Exposed] private float _duration = 1f;
    [SerializeField] private When _when = When.OnEnter;
    [Tooltip("Restart: always from Duration. Keep Longer: only if it would end later than what's left.")]
    [SerializeField] private CooldownController.StartMode _mode = CooldownController.StartMode.Restart;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new StartCooldownAction(_cooldown, _duration, _when, _mode);
    }
}

public class StartCooldownAction : StateAction
{
    private readonly CooldownSO _cooldown;
    private readonly float _duration;
    private readonly StartCooldownActionSO.When _when;
    private readonly CooldownController.StartMode _mode;
    private AnchorReference<CooldownController> _controller;

    public StartCooldownAction(
        CooldownSO cooldown,
        float duration,
        StartCooldownActionSO.When when,
        CooldownController.StartMode mode)
    {
        _cooldown = cooldown;
        _duration = duration;
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
            controller.StartCooldown(_cooldown, _duration, _mode);
        }
    }
}
