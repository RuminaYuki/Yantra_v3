using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(PlayerInteractFlagTest))]
[CreateAssetMenu(
    fileName = "NewTestInteractFlag_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Test/Test Interact Flag")]
public class TestInteractFlagConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition() => new TestInteractFlagCondition();
}

public class TestInteractFlagCondition : Condition
{
    private AnchorReference<PlayerInteractFlagTest> _interactFlagTest;

    public override void Awake(StateMachine stateMachine)
    {
        _interactFlagTest = new AnchorReference<PlayerInteractFlagTest>(stateMachine, this);
    }

    protected override bool Statement() =>
        _interactFlagTest.TryGet(out PlayerInteractFlagTest test) && test.IsInteracting;
}
