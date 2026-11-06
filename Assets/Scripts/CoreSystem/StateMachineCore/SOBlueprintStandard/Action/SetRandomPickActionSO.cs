using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Sets a pool's current pick on the character's RandomPickController when the state is entered,
// without rolling. Use it to clear an old result that Random Picked conditions would still see.
// Example: leaving Glory without being executed sets GloryRandomPool to NotGlory, so the
// "dead" any-transition (which needs NotGlory) can fire later.
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(RandomPickController))]
[CreateAssetMenu(
    fileName = "NewSetRandomPick_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Set Random Pick")]
public class SetRandomPickActionSO : StateActionSO
{
    [SerializeField] private RandomPoolSO _pool;
    [Tooltip("An option in that pool. Empty = nothing picked.")]
    [SerializeField] private RandomOptionSO _option;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetRandomPickAction(_pool, _option);
    }
}

public class SetRandomPickAction : StateAction
{
    private readonly RandomPoolSO _pool;
    private readonly RandomOptionSO _option;
    private AnchorReference<RandomPickController> _picker;

    public SetRandomPickAction(RandomPoolSO pool, RandomOptionSO option)
    {
        _pool = pool;
        _option = option;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _picker = new AnchorReference<RandomPickController>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (_picker.TryGet(out RandomPickController picker))
        {
            picker.SetCurrent(_pool, _option);
        }
    }

    public override void OnUpdate() { }

    public override void OnStateExit() { }
}
