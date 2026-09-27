using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "OnHurtByType_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Health/OnHurtByType")]
public class OnHurtByTypeConditionSO : StateConditionSO
{
    [SerializeField] private DamageTypeID damageTypeID;

    public override Condition CreateCondition()
    {
        return new OnHurtByTypeCondition(damageTypeID);
    }
}
public class OnHurtByTypeCondition : Condition
{
    private readonly DamageTypeID damageTypeID;
    private Health _health;
    private bool _wasRaised;

    public OnHurtByTypeCondition(DamageTypeID damageTypeID)
    {
        this.damageTypeID = damageTypeID;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (!stateMachine.TryGetRequired(out _health, this))
        {
            return;
        }

        _health.OnHurtDamageType += HandleHurt;
    }

    public override void OnStateEnter()
    {
        _wasRaised = false;
    }

    protected override bool Statement()
    {
        bool result = _wasRaised;
        _wasRaised = false;
        return result;
    }

    public override void Dispose()
    {
        if (_health != null)
            _health.OnHurtDamageType -= HandleHurt;

        _wasRaised = false;
    }

    private void HandleHurt(DamageTypeID damageTypeID)
    {
        if(this.damageTypeID == damageTypeID)
        _wasRaised = true;
    }
}
