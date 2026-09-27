using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "ReleaseAttackTokenAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Attack/Token/Release Attack Token")]
public class ReleaseAttackTokenActionSO : StateActionSO
{
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new ReleaseAttackTokenAction();
    }
}

public class ReleaseAttackTokenAction : StateAction
{
    private AttackTokenUser _attackTokenUser;

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _attackTokenUser, this);
    }

    public override void OnStateEnter()
    {
        if (_attackTokenUser != null)
        {
            _attackTokenUser.Release();
        }
    }

    public override void OnUpdate() { }
    public override void OnStateExit() { }
}
