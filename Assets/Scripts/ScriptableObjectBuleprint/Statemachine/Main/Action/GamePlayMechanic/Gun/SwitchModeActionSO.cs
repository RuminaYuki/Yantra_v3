using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(SkillPoints))]
[RequiresOwnerComponent(typeof(GunController))]
[CreateAssetMenu(
    fileName = "SwitchMode_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Gun/Switch Mode")]
public class SwitchModeActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SwitchModeAction();
    }
}

public class SwitchModeAction : StateAction
{
    // Self Anchor = the gun; skill points always come from the owner (the character).
    private AnchorReference<GunController> _gunController;
    private AnchorReference<SkillPoints> _skillPoints;

    public override void Awake(StateMachine stateMachine)
    {
        _gunController = new AnchorReference<GunController>(stateMachine, this);
        _skillPoints = new AnchorReference<SkillPoints>(stateMachine, null, this);
    }

    public override void OnStateEnter()
    {
        if (!_gunController.TryGet(out GunController gunController) ||
            !_skillPoints.TryGet(out SkillPoints skillPoints)) return;

        GunController.GunMode desiredMode = GunController.GunMode.Normal;
        if (skillPoints.CurrentSkillPoints >= skillPoints.MaxSkillPoints)
        {
            desiredMode = GunController.GunMode.Special;
        }

        if (gunController.GetGunMode() != desiredMode)
        {
            gunController.SwitchMode();
        }
    }

    public override void OnUpdate() { }
}
