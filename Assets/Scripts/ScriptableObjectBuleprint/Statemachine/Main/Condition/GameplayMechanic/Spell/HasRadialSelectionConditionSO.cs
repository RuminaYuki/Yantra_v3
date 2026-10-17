using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(SpellRadialMenuSelect))]
[CreateAssetMenu(
    fileName = "NewHasRadialSelection_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/GamePlayMechanic/Spell/Has Radial Selection")]
public class HasRadialSelectionConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition() => new HasRadialSelectionCondition();
}

public class HasRadialSelectionCondition : Condition
{
    private AnchorReference<SpellRadialMenuSelect> _menuSelect;

    public override void Awake(StateMachine stateMachine)
    {
        _menuSelect = new AnchorReference<SpellRadialMenuSelect>(stateMachine, this);
    }

    protected override bool Statement() =>
        _menuSelect.TryGet(out SpellRadialMenuSelect menu) && menu.HasSelection;
}

