using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// A set of actions shared by several states, so it's edited in one place. It runs them exactly as if
// they were listed in the state itself, in order. Groups don't nest: a Group inside a Group is skipped.
[CreateAssetMenu(
    fileName = "NewGroup_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/FlowControl/Group")]
public class GroupActionSO : StateActionSO
{
    [Tooltip("Run in this order, like a state's own action list. Can't contain another Group.")]
    [SerializeField] private StateActionSO[] _actions = System.Array.Empty<StateActionSO>();

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        var actions = new StateAction[_actions.Length];
        for (int i = 0; i < _actions.Length; i++)
        {
            actions[i] = Create(_actions[i], stateMachine);
        }

        return new GroupAction(actions);
    }

    // Through the Resolver, like StateSO.CreateState, so Variant and character overrides reach them.
    private StateActionSO Resolve(StateActionSO action, StateMachine stateMachine)
    {
        return stateMachine.Resolver != null ? stateMachine.Resolver.Resolve(action) : action;
    }

    private StateAction Create(StateActionSO action, StateMachine stateMachine)
    {
        if (action == null)
        {
            return null;
        }

        StateActionSO resolved = Resolve(action, stateMachine);

        // Checked after resolving, since an override could swap a Group in.
        if (resolved is GroupActionSO)
        {
            Debug.LogError($"[GroupAction] '{name}' contains a Group ('{resolved.name}'); Groups don't nest, so it's skipped.",
                stateMachine.Owner);
            return null;
        }

        return resolved.CreateAction(stateMachine);
    }
}

public class GroupAction : StateAction
{
    // Null entries are empty slots or skipped Groups.
    private readonly StateAction[] _actions;

    public GroupAction(StateAction[] actions)
    {
        _actions = actions;
    }

    public override void Awake(StateMachine stateMachine)
    {
        // The state only knows about this wrapper, so pass Awake on.
        foreach (StateAction action in _actions)
        {
            action?.Initialize(stateMachine);
        }
    }

    // A broken child is skipped on its own; the rest of the group still runs.
    public override void OnStateEnter()
    {
        foreach (StateAction action in _actions)
        {
            if (IsUsable(action))
            {
                action.OnStateEnter();
            }
        }
    }

    public override void OnUpdate()
    {
        foreach (StateAction action in _actions)
        {
            if (IsUsable(action))
            {
                action.OnUpdate();
            }
        }
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
        foreach (StateAction action in _actions)
        {
            if (IsUsable(action))
            {
                action.OnStateExit();
            }
        }
    }

    private static bool IsUsable(StateAction action)
    {
        return action != null && !action.IsDisabled;
    }
}
