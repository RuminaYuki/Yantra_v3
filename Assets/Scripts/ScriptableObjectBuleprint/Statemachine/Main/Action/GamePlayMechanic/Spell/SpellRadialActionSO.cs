using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SpellRadial_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/GamePlayMechanic/Spell/SpellRadial")]

public class SpellRadialActionSO : StateActionSO
{
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SpellRadialAction();
    }
}

public class SpellRadialAction : StateAction
{
    private SpellRadialMenuSelect _spellRadialMenuSelect;
    public override void Awake(StateMachine stateMachine)
    {
        _spellRadialMenuSelect = stateMachine.GetComponent<SpellRadialMenuSelect>();
    }
    public override void OnStateEnter()
    {
        if (_spellRadialMenuSelect == null) return;
        _spellRadialMenuSelect.HandleOpenRadial();
    }
    public override void OnUpdate(){}

    public override void OnStateExit()
    {
        if (_spellRadialMenuSelect == null) return;
        _spellRadialMenuSelect.HandleCloseRadial();
    }
}
