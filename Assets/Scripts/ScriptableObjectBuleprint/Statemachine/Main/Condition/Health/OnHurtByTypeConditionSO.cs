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
// Same as OnHurtCondition, but only for one damage type: a matching hit is true for
// exactly one state machine update, not used up by reading and never lingering.
public class OnHurtByTypeCondition : Condition
{
    private readonly DamageTypeID damageTypeID;
    private Health _health;
    private bool _pending;   // matching hit happened, waiting for the next update
    private bool _active;    // true during this update

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
        // A hit from before this state (e.g. the one that got us here) doesn't count.
        _pending = false;
        _active = false;
    }

    // Start of every update: last update's hit expires, a new one becomes visible.
    protected override void OnTick()
    {
        _active = _pending;
        _pending = false;
    }

    protected override bool Statement()
    {
        return _active;
    }

    public override void Dispose()
    {
        if (_health != null)
            _health.OnHurtDamageType -= HandleHurt;
    }

    private void HandleHurt(DamageTypeID damageTypeID)
    {
        if (this.damageTypeID == damageTypeID)
            _pending = true;
    }
}
