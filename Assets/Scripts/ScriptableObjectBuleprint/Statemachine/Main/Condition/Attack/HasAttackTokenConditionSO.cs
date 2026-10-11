using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(AttackTokenUser))]
[CreateAssetMenu(
    fileName = "HasAttackTokenCondition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Attack/Attack Token/Has Attack Token")]
public class HasAttackTokenConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new HasAttackTokenCondition();
    }
}

public class HasAttackTokenCondition : Condition
{
    private AnchorReference<AttackTokenUser> _attackTokenUser;

    public override void Awake(StateMachine stateMachine)
    {
        _attackTokenUser = new AnchorReference<AttackTokenUser>(stateMachine, this);
    }

    protected override bool Statement()
    {
        return _attackTokenUser.TryGet(out AttackTokenUser tokenUser) && tokenUser.HasToken;
    }
}
