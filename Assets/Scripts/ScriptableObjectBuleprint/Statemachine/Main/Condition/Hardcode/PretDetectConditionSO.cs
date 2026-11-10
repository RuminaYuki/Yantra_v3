using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Compares Pret's DetectPoint (PretDetectComponent) with a number,
// e.g. "DetectPoint >= 100" to go chase the player.
[RequiresOwnerComponent(typeof(PretDetectComponent))]
[CreateAssetMenu(
    fileName = "PretDetectCondition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Hardcode/Pret Detect")]
public class PretDetectConditionSO : StateConditionSO
{
    // No == / != : DetectPoint is a float that moves every frame, so it almost never equals a number exactly.
    public enum Comparison
    {
        [InspectorName("<")] Less,
        [InspectorName("<=")] LessOrEqual,
        [InspectorName(">")] Greater,
        [InspectorName(">=")] GreaterOrEqual
    }

    [SerializeField] private Comparison _comparison = Comparison.GreaterOrEqual;
    [SerializeField,Exposed] private float _value = 100f;

    protected override Condition OnCreateCondition()
    {
        return new PretDetectCondition(_comparison, _value);
    }
}

public class PretDetectCondition : Condition
{
    private readonly PretDetectConditionSO.Comparison _comparison;
    private readonly float _value;
    private AnchorReference<PretDetectComponent> _detect;

    public PretDetectCondition(PretDetectConditionSO.Comparison comparison, float value)
    {
        _comparison = comparison;
        _value = value;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _detect = new AnchorReference<PretDetectComponent>(stateMachine, this);
    }

    protected override bool Statement()
    {
        // No component (not linked yet): false, so nothing fires early.
        if (!_detect.TryGet(out PretDetectComponent detect)) return false;

        float current = detect.DetectPoint;
        return _comparison switch
        {
            PretDetectConditionSO.Comparison.Less => current < _value,
            PretDetectConditionSO.Comparison.LessOrEqual => current <= _value,
            PretDetectConditionSO.Comparison.Greater => current > _value,
            _ => current >= _value
        };
    }
}
