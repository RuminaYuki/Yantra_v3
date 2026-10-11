using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "HasPairedRequest_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Paired Animation/Has Paired Request")]
public class HasPairedRequestConditionSO : StateConditionSO
{
    [SerializeField] private PairedActionType _type;

    protected override Condition OnCreateCondition() => new HasPairedRequestCondition(_type);
}

public class HasPairedRequestCondition : Condition
{
    private readonly PairedActionType _type;
    private AnchorReference<PairedAnimationActor> _actor;

    public HasPairedRequestCondition(PairedActionType type)
    {
        _type = type;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _actor = new AnchorReference<PairedAnimationActor>(stateMachine, this);
    }

    protected override bool Statement() =>
        _actor.TryGet(out PairedAnimationActor actor) && actor.HasPending(_type);
}