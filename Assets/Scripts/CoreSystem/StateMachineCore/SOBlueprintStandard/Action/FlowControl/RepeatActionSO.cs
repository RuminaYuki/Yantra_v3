using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Runs its actions as a round, and starts them over each time Round Ends When matches: a loop inside
// one state, without a pass-through state. Rounds done are written to Counter (0 on entering), so the
// way out is a Counter Compare on the state's arrow. Count stops the looping by itself if set.
// Self Anchor is only used to find the CounterController.
[UseSelfAnchor]
[CreateAssetMenu(
    fileName = "NewRepeat_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/FlowControl/Repeat")]
public class RepeatActionSO : StateActionSO
{
    [Tooltip("One round. Run in this order, like a state's own action list. Can't contain another Repeat.")]
    [SerializeField] private StateActionSO[] _actions = System.Array.Empty<StateActionSO>();

    [Tooltip("Ends the current round, e.g. Animation Finished of the round's animation. " +
             "Empty = each round ends right away (one round a frame); needs a Count then.")]
    [SerializeField] private StateConditionSO _roundEndsWhen;

    [Tooltip("Rounds done so far: set to 0 on entering, +1 each time a round ends. Check it with Counter Compare " +
             "on the arrow out. Empty = not counted.")]
    [SerializeField] private CounterSO _counter;

    [Tooltip("Stops starting new rounds after this many; the last round's actions keep running until the state " +
             "exits. 0 = repeat until an arrow leaves the state.")]
    [SerializeField, Min(0), Exposed] private int _count;

    [Tooltip("On: the actions get Exit then Enter between rounds, as if the state was re-entered. " +
             "Off: only Enter again, so things done on Exit (e.g. Play Animator State's Exit State) " +
             "happen once, when the state exits.")]
    [SerializeField] private bool _exitEveryRound = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        var actions = new StateAction[_actions.Length];
        for (int i = 0; i < _actions.Length; i++)
        {
            actions[i] = CreateAction(_actions[i], stateMachine);
        }

        return new RepeatAction(actions, CreateCondition(stateMachine), _counter, _count, _exitEveryRound);
    }

    // Through the Resolver, like StateSO.CreateState, so Variant and character overrides reach them.
    private StateAction CreateAction(StateActionSO action, StateMachine stateMachine)
    {
        if (action == null)
        {
            return null;
        }

        StateActionSO resolved = stateMachine.Resolver != null ? stateMachine.Resolver.Resolve(action) : action;

        // Checked after resolving, since an override could swap a Repeat in.
        if (resolved is RepeatActionSO)
        {
            Debug.LogError($"[RepeatAction] '{name}' contains a Repeat ('{resolved.name}'); Repeats don't nest, so it's skipped.",
                stateMachine.Owner);
            return null;
        }

        return resolved.CreateAction(stateMachine);
    }

    private Condition CreateCondition(StateMachine stateMachine)
    {
        if (_roundEndsWhen == null)
        {
            return null;
        }

        StateConditionSO resolved = stateMachine.Resolver != null ? stateMachine.Resolver.Resolve(_roundEndsWhen) : _roundEndsWhen;
        Condition condition = resolved.CreateCondition();
        if (condition != null)
        {
            condition.DebugName = resolved.name;
        }

        return condition;
    }
}

public class RepeatAction : StateAction
{
    // Null entries are empty slots or skipped Repeats.
    private readonly StateAction[] _actions;
    private readonly Condition _roundEndsWhen;
    private readonly CounterSO _counter;
    private readonly int _count;
    private readonly bool _exitEveryRound;

    private AnchorReference<CounterController> _controller;

    private int _roundsDone;
    private int _roundStartFrame;
    // A round ended last frame; the next starts this frame, after the arrows have had a look.
    private bool _isRestartPending;
    // Count reached: no more rounds this pass.
    private bool _isDone;

    public RepeatAction(StateAction[] actions, Condition roundEndsWhen, CounterSO counter, int count, bool exitEveryRound)
    {
        _actions = actions;
        _roundEndsWhen = roundEndsWhen;
        _counter = counter;
        _count = count;
        _exitEveryRound = exitEveryRound;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (_roundEndsWhen == null)
        {
            // Rounds end right away, so only Count can stop them.
            if (_count <= 0)
            {
                Debug.LogError("[RepeatAction] has no Round Ends When and no Count, so it would repeat every frame forever.",
                    stateMachine.Owner);
                Disable();
                return;
            }
        }
        else
        {
            // Nothing else knows about the condition, so it's started and ticked here. Registered so the
            // state machine still disposes it.
            _roundEndsWhen.Initialize(stateMachine);
            stateMachine.RegisterCondition(_roundEndsWhen);
            if (_roundEndsWhen.IsDisabled)
            {
                Disable();
                return;
            }
        }

        // The state only knows about this wrapper, so pass Awake on.
        foreach (StateAction action in _actions)
        {
            action?.Initialize(stateMachine);
        }

        if (_counter != null)
        {
            // Self Anchor empty = this character's counters; set = another object's.
            _controller = new AnchorReference<CounterController>(stateMachine, this);
        }
    }

    public override void OnStateEnter()
    {
        _roundsDone = 0;
        _isRestartPending = false;
        _isDone = false;
        SetCounter(0);

        foreach (StateAction action in _actions)
        {
            if (IsUsable(action))
            {
                action.OnStateEnter();
            }
        }

        StartRoundCondition();
    }

    public override void OnUpdate()
    {
        if (_isRestartPending)
        {
            _isRestartPending = false;
            Restart();
        }

        foreach (StateAction action in _actions)
        {
            if (IsUsable(action))
            {
                action.OnUpdate();
            }
        }

        if (_isDone)
        {
            return;
        }

        if (!HasRoundEnded())
        {
            return;
        }

        _roundsDone++;
        SetCounter(_roundsDone);

        if (_count > 0 && _roundsDone >= _count)
        {
            _isDone = true;
            return;
        }

        // Not restarted now: the arrows are checked before this update, so waiting a frame lets a
        // Counter Compare leave first, instead of the next round starting for one frame.
        _isRestartPending = true;
    }

    public override void OnFixedUpdate()
    {
        foreach (StateAction action in _actions)
        {
            if (IsUsable(action))
            {
                action.OnFixedUpdate();
            }
        }
    }

    public override void OnStateExit()
    {
        _isRestartPending = false;

        foreach (StateAction action in _actions)
        {
            if (IsUsable(action))
            {
                action.OnStateExit();
            }
        }
    }

    private void Restart()
    {
        foreach (StateAction action in _actions)
        {
            if (!IsUsable(action))
            {
                continue;
            }

            if (_exitEveryRound)
            {
                action.OnStateExit();
            }

            action.OnStateEnter();
        }

        StartRoundCondition();
    }

    private void StartRoundCondition()
    {
        _roundEndsWhen?.OnStateEnter();
        _roundStartFrame = UnityEngine.Time.frameCount;
    }

    private bool HasRoundEnded()
    {
        // No condition: every round ends on its first update.
        if (_roundEndsWhen == null)
        {
            return true;
        }

        // A round's own start (e.g. a CrossFade) only shows next frame; until then the condition could
        // still see the previous round finished.
        if (UnityEngine.Time.frameCount == _roundStartFrame)
        {
            return false;
        }

        // Not in any transition, so nothing else ticks it or clears its cache.
        _roundEndsWhen.Tick();
        bool roundEnded = _roundEndsWhen.GetStatement();
        _roundEndsWhen.ClearStatementCache();
        return roundEnded;
    }

    private void SetCounter(int value)
    {
        if (_counter != null && _controller.TryGet(out CounterController controller))
        {
            controller.Set(_counter, value);
        }
    }

    private static bool IsUsable(StateAction action)
    {
        return action != null && !action.IsDisabled;
    }
}
