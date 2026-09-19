using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SpellDraw_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Spell/SpellDraw")]

public class SpellDrawActionSo : StateActionSO
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
        if (_spellDrawAnchor != null)
        {
            if (_spellDrawAnchor.Value == null)
            {
                Debug.LogWarning(
                    $"{_spellDrawAnchor.name} has not been provided a value yet; SpellDrawAction cannot resolve a SpellController."
                );
                return;
            }
            _spellController = _spellDrawAnchor.Value.gameObject.GetComponent<SpellController>();
            if (_spellController == null)
            {
                Debug.LogWarning(
                    $"SpellController component was not found on the GameObject referenced by {_spellDrawAnchor.name}."
                );
            }
            return;
        }
        _spellController = stateMachine.GetComponent<SpellController>();
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