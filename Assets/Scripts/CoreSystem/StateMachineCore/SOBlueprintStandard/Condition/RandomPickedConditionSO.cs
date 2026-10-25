using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// True when the pool's last roll (Roll Random Pool) picked this option. Leave Option empty for
// "nothing was ready", the way out when every option is on cooldown.
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(RandomPickController))]
[CreateAssetMenu(
    fileName = "NewRandomPicked_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Random Picked")]
public class RandomPickedConditionSO : StateConditionSO
{
    [SerializeField] private RandomPoolSO _pool;
    [Tooltip("An option in that pool. Empty = nothing was picked.")]
    [SerializeField] private RandomOptionSO _option;

    protected override Condition OnCreateCondition()
    {
        return new RandomPickedCondition(_pool, _option);
    }
}

public class RandomPickedCondition : Condition
{
    private readonly RandomPoolSO _pool;
    private readonly RandomOptionSO _option;
    private AnchorReference<RandomPickController> _picker;

    public RandomPickedCondition(RandomPoolSO pool, RandomOptionSO option)
    {
        _pool = pool;
        _option = option;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _picker = new AnchorReference<RandomPickController>(stateMachine, this);
    }

    protected override bool Statement()
    {
        // No pool or no picker (not linked yet): false, so nothing fires early.
        return _pool != null &&
               _picker.TryGet(out RandomPickController picker) &&
               picker.GetCurrent(_pool) == _option;
    }
}
