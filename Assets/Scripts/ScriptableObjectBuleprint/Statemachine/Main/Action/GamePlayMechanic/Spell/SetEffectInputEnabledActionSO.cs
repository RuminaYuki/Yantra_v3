using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(EffectController))]
[CreateAssetMenu(
    fileName = "SetEffectInputEnabled_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Spell/Set Effect Input Enabled")]
public class SetEffectInputEnabledActionSO : StateActionSO
{
    [SerializeField] private bool enabledOnEnter = true;
    [SerializeField] private bool enabledOnExit;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetEffectInputEnabledAction(
            enabledOnEnter,
            enabledOnExit);
    }
}

public class SetEffectInputEnabledAction : StateAction
{
    private readonly bool enabledOnEnter;
    private readonly bool enabledOnExit;
    private AnchorReference<EffectController> effectController;

    public SetEffectInputEnabledAction(
        bool enabledOnEnter,
        bool enabledOnExit)
    {
        this.enabledOnEnter = enabledOnEnter;
        this.enabledOnExit = enabledOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        effectController = new AnchorReference<EffectController>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!effectController.TryGet(out EffectController controller)) return;
        controller.SetEnabled(enabledOnEnter);
    }

    public override void OnStateExit()
    {
        if (!effectController.TryGet(out EffectController controller)) return;
        controller.SetEnabled(enabledOnExit);
    }

    public override void OnUpdate()
    {
    }
}
