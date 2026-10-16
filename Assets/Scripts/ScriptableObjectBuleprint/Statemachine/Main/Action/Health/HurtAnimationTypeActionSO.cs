using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(DamageTypeAnimationActor))]
[CreateAssetMenu(
    fileName = "HurtAnimationTypeAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Health/HurtAnimationTypeAction")]

public class HurtAnimationTypeActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new HurtAnimationTypeAction();
    }
}
public class HurtAnimationTypeAction : StateAction
{
    private AnchorReference<DamageTypeAnimationActor> _damageTypeAnimationActor;
    public override void Awake(StateMachine stateMachine)
    {
        _damageTypeAnimationActor = new AnchorReference<DamageTypeAnimationActor>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        if (!_damageTypeAnimationActor.TryGet(out DamageTypeAnimationActor actor)) return;
        actor.PlayAnimationWithDamageType();
    }
    public override void OnUpdate(){}
    public override void OnStateExit()
    {
    }
}
