using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "SetIgnoreDamage_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Health/Set Ignore Damage")]
public class SetIgnoreDamageActionSO : StateActionSO
{
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetIgnoreDamageAction(value, resetValueOnExit);
    }
}

public class SetIgnoreDamageAction : StateAction
{
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private Health health;

    public SetIgnoreDamageAction(bool value, bool resetValueOnExit)
    {
        this.value = value;
        this.resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out health, this);
    }

    public override void OnStateEnter()
    {
        health.SetEnableIgnoreDamage(value);
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (!resetValueOnExit) return;
        health.SetEnableIgnoreDamage(!value);
    }
}
