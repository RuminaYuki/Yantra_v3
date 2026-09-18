using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "ReleaseAttackTokenAction",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Attack/Token/Release Attack Token")]
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
        _attackTokenUser = stateMachine.GetComponent<AttackTokenUser>();

        if (_attackTokenUser == null)
        {
            Debug.LogError(
                "ReleaseAttackTokenAction cannot find AttackTokenUser.");
        }
    }

    public override void OnStateEnter()
    {
        _attackTokenUser?.Release();
    }

    public override void OnUpdate() { }
    public override void OnStateExit() { }
}
