using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(DamageTypeAnimationActor))]
[CreateAssetMenu(
    fileName = "HurtAnimationTypeAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Health/HurtAnimationTypeAction")]

public class HurtAnimationTypeActionSO : StateActionSO
{
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new HurtAnimationTypeAction();
    }
}
public class HurtAnimationTypeAction : StateAction
{
    private DamageTypeAnimationActor _damageTypeAnimationActor;
    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _damageTypeAnimationActor, this);
    }
    public override void OnStateEnter()
    {
        _damageTypeAnimationActor.PlayAnimationWithDamageType();
    }
    public override void OnUpdate(){}
    public override void OnStateExit()
    {
    }
}
