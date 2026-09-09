using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewNextState_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/Next State")]
public class NextStateConditionSO : StateConditionSO
{
    public override Condition CreateCondition()
    {
        return new NextStateCondition();
    }
}

public class NextStateCondition : Condition
{
    protected override bool Statement()
    {
        return true;
    }
}