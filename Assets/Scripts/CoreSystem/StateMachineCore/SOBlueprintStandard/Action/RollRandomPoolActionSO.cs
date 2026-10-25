using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Rolls one pool of the character's RandomPickController when the state is entered. The transitions
// out check the result with Random Picked (RandomPickedConditionSO).
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(RandomPickController))]
[CreateAssetMenu(
    fileName = "NewRollRandomPool_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Roll Random Pool")]
public class RollRandomPoolActionSO : StateActionSO
{
    [SerializeField] private RandomPoolSO _pool;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new RollRandomPoolAction(_pool);
    }
}

public class RollRandomPoolAction : StateAction
{
    private readonly RandomPoolSO _pool;
    private AnchorReference<RandomPickController> _picker;

    public RollRandomPoolAction(RandomPoolSO pool)
    {
        _pool = pool;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _picker = new AnchorReference<RandomPickController>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (_picker.TryGet(out RandomPickController picker))
        {
            picker.Roll(_pool);
        }
    }

    public override void OnUpdate() { }

    public override void OnStateExit() { }
}
