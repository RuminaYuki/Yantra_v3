using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "SetHealthFloor_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Health/Set Health Floor")]
public class SetHealthFloorActionSO : StateActionSO
{
    [Tooltip("Percent of Max Health a hit can't go below while HP is above it (see Health). 0 = off. " +
             "Bind it to the same parameter as the Health Percent condition that starts Glory.")]
    [SerializeField, Range(0f, 100f), Exposed] private float _percent = 20f;

    [Tooltip("Put the old floor back when the state is left. Off on the starting state, so the floor stays.")]
    [SerializeField] private bool _resetOnStateExit = false;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetHealthFloorAction(_percent, _resetOnStateExit);
    }
}

public class SetHealthFloorAction : StateAction
{
    private readonly float _percent;
    private readonly bool _resetOnStateExit;
    private AnchorReference<Health> _health;
    private float _previousPercent;
    private bool _isApplied;

    public SetHealthFloorAction(float percent, bool resetOnStateExit)
    {
        _percent = percent;
        _resetOnStateExit = resetOnStateExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _health = new AnchorReference<Health>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_health.TryGet(out Health health)) return;
        _previousPercent = health.MinHealthPercent;
        health.SetMinHealthPercent(_percent);
        _isApplied = true;
    }

    public override void OnStateExit()
    {
        if (!_isApplied || !_health.TryGet(out Health health))
            return;

        if (_resetOnStateExit)
            health.SetMinHealthPercent(_previousPercent);

        _isApplied = false;
    }

    public override void OnUpdate() { }
}
