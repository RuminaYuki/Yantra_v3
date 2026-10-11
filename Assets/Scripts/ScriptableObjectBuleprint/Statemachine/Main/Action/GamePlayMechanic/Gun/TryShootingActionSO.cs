using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(GunController))]
[CreateAssetMenu(
    fileName = "TryShooting_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Gun/Try Shooting")]
public class TryShootingActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new TryShootingAction();
    }
}

public class TryShootingAction : StateAction
{
    private AnchorReference<GunController> _gunController;

    public override void Awake(StateMachine stateMachine)
    {
        _gunController = new AnchorReference<GunController>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_gunController.TryGet(out GunController gunController)) return;
        gunController.TryShooting();
    }

    public override void OnUpdate() { }
}
