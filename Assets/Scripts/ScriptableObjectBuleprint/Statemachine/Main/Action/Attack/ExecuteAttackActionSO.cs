using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "ExecuteAttack_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Attack/Execute Attack")]
public class ExecuteAttackActionSO : StateActionSO
{
    
    [SerializeField] private AttackParameters _attackParameters;

    public AttackParameters AttackParameters
    {
        get => _attackParameters;
        set
        {
            if (value.damageAmount < 0f)
            {
                Debug.LogWarning("Damage amount cannot be negative. Setting to 0.");
                value.damageAmount = 0f;
            }
            if (value.attackRange < 0f)
            {
                Debug.LogWarning("Attack range cannot be negative. Setting to 0.");
                value.attackRange = 0f;
            }
            if (value.attackRadius < 0f)
            {
                Debug.LogWarning("Attack radius cannot be negative. Setting to 0.");
                value.attackRadius = 0f;
            }
            _attackParameters = value;
        }
    }

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new ExecuteAttackAction(_attackParameters);
    }
}

public class ExecuteAttackAction : StateAction
{
    private readonly AttackParameters _attackParameters;

    private AttackSphereCast _attackSphereCast;

    public ExecuteAttackAction(AttackParameters attackParameters)
    {
        _attackParameters = attackParameters;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _attackSphereCast = stateMachine.GetComponent<AttackSphereCast>();

        if (_attackSphereCast == null)
            Debug.LogError(
                "ExecuteAttackAction requires AttackSphereCast.",
                stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        if (_attackSphereCast == null)
            return;

        _attackSphereCast.TryToExecuteAttack(_attackParameters);
    }

    public override void OnUpdate() { }
}
