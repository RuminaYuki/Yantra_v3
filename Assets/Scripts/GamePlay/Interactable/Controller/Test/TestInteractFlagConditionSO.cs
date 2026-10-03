using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(PlayerInteractFlagTest))]
[CreateAssetMenu(
    fileName = "NewTestInteractFlag_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Test/Test Interact Flag")]
public class TestInteractFlagConditionSO : StateConditionSO
{
    public override Condition CreateCondition() => new TestInteractFlagCondition();
}

public class TestInteractFlagCondition : Condition
{
    private PlayerInteractFlagTest _interactFlagTest;

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _interactFlagTest, this);
    }

    protected override bool Statement() => _interactFlagTest.IsInteracting;
}
