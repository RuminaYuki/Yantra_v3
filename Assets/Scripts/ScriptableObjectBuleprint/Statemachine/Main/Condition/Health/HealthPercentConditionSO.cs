using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "NewHealthPercent_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Health/Health Percent")]
public class HealthPercentConditionSO : StateConditionSO
{
    public enum Compare
    {
        [Tooltip("True once HP drops to the percent or lower (e.g. a boss phase at 50%).")]
        AtOrBelow,

        [Tooltip("True while HP is at the percent or higher (e.g. healed back above 80%).")]
        AtOrAbove
    }

    [Tooltip("Percent of Max Health, 0-100.")]
    [SerializeField, Range(0f, 100f), Exposed] private float _percent = 50f;
    [SerializeField] private Compare _compare = Compare.AtOrBelow;

    protected override Condition OnCreateCondition()
    {
        return new HealthPercentCondition(_percent, _compare);
    }
}

// Like OnDeadCondition, it reads Health every check instead of listening for OnHealthChanged,
// so it can't miss a change and stays true for as long as HP is past the line.
public class HealthPercentCondition : Condition
{
    private readonly float _percent;
    private readonly HealthPercentConditionSO.Compare _compare;
    private AnchorReference<Health> _health;

    public HealthPercentCondition(float percent, HealthPercentConditionSO.Compare compare)
    {
        _percent = percent;
        _compare = compare;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _health = new AnchorReference<Health>(stateMachine, this);
    }

    protected override bool Statement()
    {
        if (!_health.TryGet(out Health health) || health.MaxHealth <= 0f) return false;

        float currentPercent = health.CurrentHP / health.MaxHealth * 100f;
        return _compare == HealthPercentConditionSO.Compare.AtOrBelow
            ? currentPercent <= _percent
            : currentPercent >= _percent;
    }
}
