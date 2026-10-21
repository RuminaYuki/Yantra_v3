using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(GunController))]
[CreateAssetMenu(
    fileName = "Reload_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Gun/Reload")]
public class ReloadActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new ReloadAction();
    }
}

public class ReloadAction : StateAction
{
    private AnchorReference<GunController> _gunController;

    public override void Awake(StateMachine stateMachine)
    {
        _gunController = new AnchorReference<GunController>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_gunController.TryGet(out GunController gunController)) return;
        //gunController.Reload();
    }

    public override void OnUpdate() { }
}
