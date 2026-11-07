using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Put this on a state where the player must not open the inventory
// (e.g. Spell_State: picking or casting a spell). Pair it with
// "Is Inventory Blocked" = false on the transition into Inventory_State.
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PlayerInventory))]
[CreateAssetMenu(
    fileName = "NewBlockInventory_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Inventory/Block Inventory")]
public class BlockInventoryActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new BlockInventoryAction();
    }
}

public class BlockInventoryAction : StateAction
{
    private AnchorReference<PlayerInventory> inventoryRef;

    public override void Awake(StateMachine stateMachine)
    {
        inventoryRef = new AnchorReference<PlayerInventory>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!inventoryRef.TryGet(out PlayerInventory inventory)) return;
        inventory.AddOpenBlocker(this);
    }

    public override void OnStateExit()
    {
        if (!inventoryRef.TryGet(out PlayerInventory inventory)) return;
        inventory.RemoveOpenBlocker(this);
    }

    public override void OnUpdate() { }
}
