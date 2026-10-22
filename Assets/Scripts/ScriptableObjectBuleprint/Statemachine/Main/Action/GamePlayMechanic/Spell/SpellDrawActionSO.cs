using UnityEngine;
using UnityEngine.InputSystem;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(SpellController))]
[CreateAssetMenu(
    fileName = "SpellDraw_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Spell/SpellDraw")]

public class SpellDrawActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SpellDrawAction();
    }
}

public class SpellDrawAction : StateAction
{
    private AnchorReference<SpellController> _spellController;

    public override void Awake(StateMachine stateMachine)
    {
        _spellController = new AnchorReference<SpellController>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        if (!_spellController.TryGet(out SpellController spellController)) return;
        spellController.SetActive(true);
    }
    public override void OnUpdate()
    {
        if (!_spellController.TryGet(out SpellController spellController)) return;
        spellController.HandleStroke(Mouse.current.delta.ReadValue());
    }

    public override void OnStateExit()
    {
        if (!_spellController.TryGet(out SpellController spellController)) return;
        spellController.SetActive(false);
    }
}