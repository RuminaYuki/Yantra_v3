using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "PairedAnimationFinished_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Paired Animation/Paired Animation Finished")]
public class PairedAnimationFinishedConditionSO : StateConditionSO
{
    public override Condition CreateCondition() => new PairedAnimationFinishedCondition();
}

public class PairedAnimationFinishedCondition : Condition
{
    private PairedAnimationActor _actor;

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _actor, this);
    }

    protected override bool Statement() => _actor.IsFinished;
}