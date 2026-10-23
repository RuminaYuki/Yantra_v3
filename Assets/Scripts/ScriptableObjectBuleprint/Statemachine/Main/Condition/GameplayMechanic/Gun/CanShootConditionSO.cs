using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(GunController))]
[CreateAssetMenu(
    fileName = "NewCanShoot_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/GamePlayMechanic/Gun/Can Shoot")]
public class CanShootConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new CanShootCondition();
    }
}

// True while GunController.CanShoot() is true: delay finished, has bullet item, and (Special mode) enough skill points.
// It's a state, not an event, so it's just read when checked: no Tick needed.
public class CanShootCondition : Condition
{
    private AnchorReference<GunController> gunController;

    public override void Awake(StateMachine stateMachine)
    {
        gunController = new AnchorReference<GunController>(stateMachine, this);
    }

    protected override bool Statement()
    {
        return gunController.TryGet(out GunController gun) && gun.CanShoot();
    }
}
