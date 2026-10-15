using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PlayerInventory))]
[CreateAssetMenu(
    fileName = "NewOpenInventorySO_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Inventory/Open Inventory")]
public class OpenInventorySO : StateActionSO
{
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new OpenInventory(value, resetValueOnExit);
    }
}

public class OpenInventory : StateAction
{
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private AnchorReference<PlayerInventory> inventoryRef;

    public OpenInventory(bool value, bool resetValueOnExit)
    {
        this.value = value;
        this.resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        inventoryRef = new AnchorReference<PlayerInventory>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        SetOpen(value);
    }
    public override void OnStateExit()
    {
        if (!resetValueOnExit) return;
        SetOpen(!value);
    }
    public override void OnUpdate(){}

    private void SetOpen(bool open)
    {
        if (!inventoryRef.TryGet(out PlayerInventory inventory)) return;
        inventory.OpenInventory(open);
    }
}
