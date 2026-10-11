using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(AttackTokenUser))]
[CreateAssetMenu(
    fileName = "ReleaseAttackTokenAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Attack/Token/Release Attack Token")]
public class ReleaseAttackTokenActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new ReleaseAttackTokenAction();
    }
}

public class ReleaseAttackTokenAction : StateAction
{
    private AnchorReference<AttackTokenUser> _attackTokenUser;

    public override void Awake(StateMachine stateMachine)
    {
        _attackTokenUser = new AnchorReference<AttackTokenUser>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_attackTokenUser.TryGet(out AttackTokenUser tokenUser)) return;
        tokenUser.Release();
    }

    public override void OnUpdate() { }
    public override void OnStateExit() { }
}
