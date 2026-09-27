using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SpellDraw_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Spell/SpellDraw")]

public class SpellDrawActionSO : StateActionSO
{
    [SerializeField] private GameObjectAnchor _spellDrawAnchor;
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SpellDrawAction(_spellDrawAnchor);
    }
}

public class SpellDrawAction : StateAction
{
    private readonly GameObjectAnchor _spellDrawAnchor;
    private SpellController _spellController;
    public SpellDrawAction(GameObjectAnchor spellDrawAnchor)
    {
        _spellDrawAnchor = spellDrawAnchor;
    }
    public override void Awake(StateMachine stateMachine)
    {
        _spellDrawAnchor.TryGetComponentOrOwner(stateMachine, out _spellController, this);
    }
    public override void OnStateEnter()
    {
        if (_spellController == null) return;
        _spellController.SetActive(true);
    }
    public override void OnUpdate(){}

    public override void OnStateExit()
    {
        if (_spellController == null) return;
        _spellController.SetActive(false);
    }
}