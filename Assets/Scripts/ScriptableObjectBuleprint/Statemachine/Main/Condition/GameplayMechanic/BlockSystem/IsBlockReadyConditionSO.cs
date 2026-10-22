using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(BlockSystem))]
[CreateAssetMenu(
    fileName = "NewIsBlockReady_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/GamePlayMechanic/BlockSystem/Is Block Ready")]
public class IsBlockReadyConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new IsBlockReadyCondition();
    }
}

// True while BlockSystem.IsReady is true: false once guard hits 0, true again only when guard is full.
// It's a state, not an event, so it's just read when checked: no Tick needed.
public class IsBlockReadyCondition : Condition
{
    private AnchorReference<BlockSystem> blockSystem;

    public override void Awake(StateMachine stateMachine)
    {
        blockSystem = new AnchorReference<BlockSystem>(stateMachine, this);
    }

    protected override bool Statement()
    {
        return blockSystem.TryGet(out BlockSystem block) && block.IsReady;
    }
}
