using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PlayerInventory))]
[CreateAssetMenu(
    fileName = "NewIsInventoryBlocked_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/GamePlayMechanic/Inventory/Is Inventory Blocked")]
public class IsInventoryBlockedConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new IsInventoryBlockedCondition();
    }
}

// True while some state has blocked the inventory (see BlockInventoryActionSO).
public class IsInventoryBlockedCondition : Condition
{
    private AnchorReference<PlayerInventory> inventoryRef;

    public override void Awake(StateMachine stateMachine)
    {
        inventoryRef = new AnchorReference<PlayerInventory>(stateMachine, this);
    }

    protected override bool Statement()
    {
        return inventoryRef.TryGet(out PlayerInventory inventory) && inventory.IsOpenBlocked;
    }
}
