using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(SpellRadialMenuSelect))]
[CreateAssetMenu(
    fileName = "NewHasRadialSelection_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/GamePlayMechanic/Spell/Has Radial Selection")]
public class HasRadialSelectionConditionSO : StateConditionSO
{
    public override Condition CreateCondition() => new HasRadialSelectionCondition();
}

public class HasRadialSelectionCondition : Condition
{
    private SpellRadialMenuSelect _menuSelect;

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _menuSelect, this);
    }

    protected override bool Statement() => _menuSelect.HasSelection;
}

