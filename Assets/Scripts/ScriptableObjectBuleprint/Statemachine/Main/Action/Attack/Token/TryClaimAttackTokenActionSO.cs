using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(AttackTokenUser))]
[CreateAssetMenu(
    fileName = "TryClaimAttackToken_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Attack/Token/Try Claim Attack Token")]
public class TryClaimAttackTokenActionSO : StateActionSO
{
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new TryClaimAttackTokenAction();
    }
}

public class TryClaimAttackTokenAction : StateAction
{
    private AttackTokenUser _attackTokenUser;

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _attackTokenUser, this);
    }

    public override void OnStateEnter()
    {
        _attackTokenUser.TryClaim();
    }

    public override void OnUpdate()
    {
        if (_attackTokenUser.HasToken)
        {
            return;
        }

        _attackTokenUser.TryClaim();
    }

    public override void OnStateExit() { }
}
