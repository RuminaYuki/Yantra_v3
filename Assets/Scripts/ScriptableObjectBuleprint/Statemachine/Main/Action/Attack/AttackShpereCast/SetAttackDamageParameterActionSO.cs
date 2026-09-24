using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewSetADParameter_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Attack/AttackShpereCast/Set Attack Damage Parameter")]
public class SetAttackDamageParameterActionSO : StateActionSO
{
    [SerializeField] private AttackParameters _attackParameters;
    [Header("Damage Type (Optional)")]
    [Tooltip("Not required - leave empty if this attack doesn't need a specific damage type.")]
    [SerializeField] private DamageTypeID _damageType;

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

    public DamageTypeID DamageType
    {
        get => _damageType;
        set => _damageType = value;
    }

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetAttackDamageParameterAction(_attackParameters, _damageType);
    }
}

public class SetAttackDamageParameterAction : StateAction
{
    private readonly AttackParameters _attackParameters;
    private readonly DamageTypeID _damageType;

    private AttackSphereCast _attackSphereCast;

    public SetAttackDamageParameterAction(
        AttackParameters attackParameters,
        DamageTypeID damageType)
    {
        _attackParameters = attackParameters;
        _damageType = damageType;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _attackSphereCast = stateMachine.GetComponent<AttackSphereCast>();

        if (_attackSphereCast == null)
            Debug.LogError(
                "SetAttackDamageParameterAction requires AttackSphereCast.",
                stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        _attackSphereCast?.SetDamageParameter(_attackParameters, _damageType);
    }

    public override void OnUpdate() { }
}
