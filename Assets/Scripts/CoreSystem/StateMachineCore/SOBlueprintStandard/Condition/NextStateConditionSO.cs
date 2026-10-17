using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewNextState_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Next State")]
public class NextStateConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
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