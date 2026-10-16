using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "PairedAnimationFinished_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Paired Animation/Paired Animation Finished")]
public class PairedAnimationFinishedConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition() => new PairedAnimationFinishedCondition();
}

public class PairedAnimationFinishedCondition : Condition
{
    private AnchorReference<PairedAnimationActor> _actor;

    public override void Awake(StateMachine stateMachine)
    {
        _actor = new AnchorReference<PairedAnimationActor>(stateMachine, this);
    }

    protected override bool Statement() =>
        _actor.TryGet(out PairedAnimationActor actor) && actor.IsFinished;
}