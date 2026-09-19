using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "HasAttackTokenCondition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Attack/Attack Token/Has Attack Token")]
public class HasAttackTokenConditionSO : StateConditionSO
{
    public override Condition CreateCondition()
    {
        return new HasAttackTokenCondition();
    }
}

public class HasAttackTokenCondition : Condition
{
    private AttackTokenUser _attackTokenUser;

    public override void Awake(StateMachine stateMachine)
    {
        _attackTokenUser = stateMachine.GetComponent<AttackTokenUser>();

        if (_attackTokenUser == null)
        {
            Debug.LogError(
                "HasAttackTokenCondition cannot find AttackTokenUser.");
        }
    }

    protected override bool Statement()
    {
        return _attackTokenUser != null &&
               _attackTokenUser.HasToken;
    }
}
