using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(AttackSphereCast))]
[CreateAssetMenu(
    fileName = "ContinuousAttack_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Attack/AttackShpereCast/Continuous Attack")]
public class ContinuousAttackActionSO : StateActionSO
{
    [SerializeField] private AttackParameters _attackParameters;
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

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new ContinuousAttackAction(_attackParameters, _damageType);
    }
}

// Checks for a hit every update while in the state, but deals damage only once per state entry
// (e.g. a charge that should hit the player once, not every frame they overlap).
public class ContinuousAttackAction : StateAction
{
    private readonly AttackParameters _attackParameters;
    private readonly DamageTypeID _damageType;

    private AnchorReference<AttackSphereCast> _attackSphereCast;
    private bool _hasHit;

    public ContinuousAttackAction(AttackParameters attackParameters, DamageTypeID damageType)
    {
        _attackParameters = attackParameters;
        _damageType = damageType;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _attackSphereCast = new AnchorReference<AttackSphereCast>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        _hasHit = false;
    }

    public override void OnUpdate()
    {
        if (_hasHit || !_attackSphereCast.TryGet(out AttackSphereCast attackSphereCast))
            return;

        _hasHit = attackSphereCast.TryToExecuteAttack(_attackParameters, _damageType);
    }
}
