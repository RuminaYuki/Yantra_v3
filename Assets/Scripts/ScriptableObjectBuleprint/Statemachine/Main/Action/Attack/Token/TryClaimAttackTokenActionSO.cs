using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(AttackTokenUser))]
[CreateAssetMenu(
    fileName = "TryClaimAttackToken_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Attack/Token/Try Claim Attack Token")]
public class TryClaimAttackTokenActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new TryClaimAttackTokenAction();
    }
}

public class TryClaimAttackTokenAction : StateAction
{
    private AnchorReference<AttackTokenUser> _attackTokenUser;

    public override void Awake(StateMachine stateMachine)
    {
        _attackTokenUser = new AnchorReference<AttackTokenUser>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_attackTokenUser.TryGet(out AttackTokenUser tokenUser)) return;
        tokenUser.TryClaim();
    }

    public override void OnUpdate()
    {
        if (!_attackTokenUser.TryGet(out AttackTokenUser tokenUser) || tokenUser.HasToken)
        {
            return;
        }

        tokenUser.TryClaim();
    }

    public override void OnStateExit() { }
}
