using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// True when the cooldown is over (or was never started). Set the transition's Expected to False
// for "still on cooldown".
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(CooldownController))]
[CreateAssetMenu(
    fileName = "NewCooldownReady_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Cooldown Ready")]
public class CooldownReadyConditionSO : StateConditionSO
{
    [SerializeField] private CooldownSO _cooldown;

    protected override Condition OnCreateCondition()
    {
        return new CooldownReadyCondition(_cooldown);
    }
}

public class CooldownReadyCondition : Condition
{
    private readonly CooldownSO _cooldown;
    private AnchorReference<CooldownController> _controller;

    public CooldownReadyCondition(CooldownSO cooldown)
    {
        _cooldown = cooldown;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _controller = new AnchorReference<CooldownController>(stateMachine, this);
    }

    protected override bool Statement()
    {
        // No controller (not linked yet): not ready, so nothing fires early.
        return _cooldown != null &&
               _controller.TryGet(out CooldownController controller) &&
               controller.IsReady(_cooldown);
    }
}
