using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetEffectInputEnabled_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Spell/Set Effect Input Enabled")]
public class SetEffectInputEnabledActionSO : StateActionSO
{
    [SerializeField] private bool enabledOnEnter = true;
    [SerializeField] private bool enabledOnExit;

    public override StateAction CreateAction(StateMachine stateMachine)
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
    private EffectController effectController;

    public SetEffectInputEnabledAction(
        bool enabledOnEnter,
        bool enabledOnExit)
    {
        this.enabledOnEnter = enabledOnEnter;
        this.enabledOnExit = enabledOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        effectController = stateMachine.GetComponent<EffectController>();
        if (effectController == null)
        {
            Debug.LogError(
                "SetEffectInputEnabledAction cannot find EffectController.");
        }
    }

    public override void OnStateEnter()
    {
        effectController?.SetEnabled(enabledOnEnter);
    }

    public override void OnStateExit()
    {
        effectController?.SetEnabled(enabledOnExit);
    }

    public override void OnUpdate()
    {
    }
}
