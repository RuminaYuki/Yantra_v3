using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Sets or adds to a counter when the state is entered (or left). Check it with
// Counter Compare (CounterConditionSO). For a loop: Set 0 before it, Add 1 in the repeated state.
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(CounterController))]
[CreateAssetMenu(
    fileName = "NewChangeCounter_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Change Counter")]
public class ChangeCounterActionSO : StateActionSO
{
    public enum Operation
    {
        Set,
        Add
    }

    public enum When
    {
        OnEnter,
        OnExit
    }

    [SerializeField] private CounterSO _counter;
    [SerializeField] private Operation _operation = Operation.Add;
    [Tooltip("Set: the new value. Add: how much to add (negative to count down).")]
    [SerializeField, Exposed] private int _value = 1;
    [SerializeField] private When _when = When.OnEnter;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new ChangeCounterAction(_counter, _operation, _value, _when);
    }
}

public class ChangeCounterAction : StateAction
{
    private readonly CounterSO _counter;
    private readonly ChangeCounterActionSO.Operation _operation;
    private readonly int _value;
    private readonly ChangeCounterActionSO.When _when;
    private AnchorReference<CounterController> _controller;

    public ChangeCounterAction(
        CounterSO counter,
        ChangeCounterActionSO.Operation operation,
        int value,
        ChangeCounterActionSO.When when)
    {
        _counter = counter;
        _operation = operation;
        _value = value;
        _when = when;
    }

    public override void Awake(StateMachine stateMachine)
    {
        // Self Anchor empty = this character's counters; set = another object's.
        _controller = new AnchorReference<CounterController>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (_when == ChangeCounterActionSO.When.OnEnter)
        {
            Change();
        }
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (_when == ChangeCounterActionSO.When.OnExit)
        {
            Change();
        }
    }

    private void Change()
    {
        if (_counter == null || !_controller.TryGet(out CounterController controller))
        {
            return;
        }

        if (_operation == ChangeCounterActionSO.Operation.Set)
        {
            controller.Set(_counter, _value);
        }
        else
        {
            controller.Add(_counter, _value);
        }
    }
}
