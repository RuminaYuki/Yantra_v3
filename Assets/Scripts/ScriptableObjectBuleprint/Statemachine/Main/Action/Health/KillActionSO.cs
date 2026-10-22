using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "NewKill_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Health/Kill")]
public class KillActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new KillAction();
    }
}

// Kills the owner on state enter through Health.Kill(), so OnDead fires like a normal death.
public class KillAction : StateAction
{
    private AnchorReference<Health> healthRef;

    public override void Awake(StateMachine stateMachine)
    {
        healthRef = new AnchorReference<Health>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!healthRef.TryGet(out Health health)) return;
        health.Kill();
    }

    public override void OnUpdate() { }

    public override void OnStateExit() { }
}
