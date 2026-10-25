using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Compares a counter (changed by Change Counter, ChangeCounterActionSO) with a number,
// e.g. "Shots >= 3" to leave a loop.
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(CounterController))]
[CreateAssetMenu(
    fileName = "NewCounterCompare_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Counter Compare")]
public class CounterConditionSO : StateConditionSO
{
    public enum Comparison
    {
        [InspectorName("==")] Equal,
        [InspectorName("!=")] NotEqual,
        [InspectorName("<")] Less,
        [InspectorName("<=")] LessOrEqual,
        [InspectorName(">")] Greater,
        [InspectorName(">=")] GreaterOrEqual
    }

    [SerializeField] private CounterSO _counter;
    [SerializeField] private Comparison _comparison = Comparison.GreaterOrEqual;
    [SerializeField, Exposed] private int _value = 1;

    protected override Condition OnCreateCondition()
    {
        return new CounterCondition(_counter, _comparison, _value);
    }
}

public class CounterCondition : Condition
{
    private readonly CounterSO _counter;
    private readonly CounterConditionSO.Comparison _comparison;
    private readonly int _value;
    private AnchorReference<CounterController> _controller;

    public CounterCondition(CounterSO counter, CounterConditionSO.Comparison comparison, int value)
    {
        _counter = counter;
        _comparison = comparison;
        _value = value;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _controller = new AnchorReference<CounterController>(stateMachine, this);
    }

    protected override bool Statement()
    {
        // No controller (not linked yet): false, so nothing fires early.
        if (_counter == null || !_controller.TryGet(out CounterController controller))
        {
            return false;
        }

        int current = controller.Get(_counter);
        return _comparison switch
        {
            CounterConditionSO.Comparison.Equal => current == _value,
            CounterConditionSO.Comparison.NotEqual => current != _value,
            CounterConditionSO.Comparison.Less => current < _value,
            CounterConditionSO.Comparison.LessOrEqual => current <= _value,
            CounterConditionSO.Comparison.Greater => current > _value,
            _ => current >= _value
        };
    }
}
