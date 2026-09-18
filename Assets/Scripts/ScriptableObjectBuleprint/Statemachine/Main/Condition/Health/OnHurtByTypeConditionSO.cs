using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "OnHurtByType_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Health/OnHurtByType")]
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
        _health = stateMachine.GetComponent<Health>();

        if (_health == null)
        {
            Debug.LogError("OnHurtCondition requires a Health component on the owner.");
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
