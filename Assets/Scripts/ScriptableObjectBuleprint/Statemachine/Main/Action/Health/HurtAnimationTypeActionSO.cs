using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

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
        _damageTypeAnimationActor = stateMachine.GetComponent<DamageTypeAnimationActor>();
    }
    public override void OnStateEnter()
    {
        if(_damageTypeAnimationActor == null) return;
        _damageTypeAnimationActor.PlayAnimationWithDamageType();
    }
    public override void OnUpdate(){}
    public override void OnStateExit()
    {
    }
}
