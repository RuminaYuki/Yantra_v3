using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(SpellRadialMenuSelect))]
[CreateAssetMenu(
    fileName = "SpellRadial_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Spell/SpellRadial")]

public class SpellRadialActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SpellRadialAction();
    }
}

public class SpellRadialAction : StateAction
{
    private AnchorReference<SpellRadialMenuSelect> _spellRadialMenuSelect;
    public override void Awake(StateMachine stateMachine)
    {
        _spellRadialMenuSelect = new AnchorReference<SpellRadialMenuSelect>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        if (!_spellRadialMenuSelect.TryGet(out SpellRadialMenuSelect menu)) return;
        menu.HandleOpenRadial();
    }
    public override void OnUpdate(){}

    public override void OnStateExit()
    {
        if (!_spellRadialMenuSelect.TryGet(out SpellRadialMenuSelect menu)) return;
        menu.HandleStateExit();
    }
}