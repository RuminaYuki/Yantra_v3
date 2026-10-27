using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// An action that can tell, after its OnStateEnter, that it couldn't do its job
// (e.g. a teleport that found no free spot). Failed means it changed nothing, so nothing needs undoing.
// Fallback Action reads it.
public interface IFallibleAction
{
    bool Failed { get; }
}

// Runs Primary on enter; if Primary says it failed (IFallibleAction), runs Fallback in the same frame
// instead. Updates and exit go to whichever one ran. Fallback can be another Fallback Action, for a chain.
[CreateAssetMenu(
    fileName = "NewFallback_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/FlowControl/Fallback")]
public class FallbackActionSO : StateActionSO
{
    [Tooltip("Tried first. Needs to be an action that can fail (IFallibleAction), or Fallback never runs.")]
    [SerializeField] private StateActionSO _primary;

    [Tooltip("Run when Primary fails.")]
    [SerializeField] private StateActionSO _fallback;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new FallbackAction(Create(_primary, stateMachine), Create(_fallback, stateMachine));
    }

    // Through the Resolver, like StateSO.CreateState, so Variant and character overrides reach them.
    private static StateAction Create(StateActionSO action, StateMachine stateMachine)
    {
        if (action == null)
        {
            return null;
        }

        StateActionSO resolved = stateMachine.Resolver != null ? stateMachine.Resolver.Resolve(action) : action;
        return resolved.CreateAction(stateMachine);
    }
}

public class FallbackAction : StateAction, IFallibleAction
{
    private readonly StateAction _primary;
    private readonly StateAction _fallback;

    // The one that ran on enter (gets the updates and the exit), or null.
    private StateAction _running;

    public bool Failed { get; private set; }

    public FallbackAction(StateAction primary, StateAction fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public override void Awake(StateMachine stateMachine)
    {
        // The state only knows about this wrapper, so pass Awake on.
        _primary?.Initialize(stateMachine);
        _fallback?.Initialize(stateMachine);

        if (_primary == null)
        {
            Debug.LogWarning("[FallbackAction] has no Primary: only Fallback runs.", stateMachine.Owner);
        }
        else if (_primary is not IFallibleAction)
        {
            Debug.LogWarning(
                $"[FallbackAction] Primary ({_primary.GetType().Name}) can't fail, so Fallback never runs.",
                stateMachine.Owner);
        }
    }

    public override void OnStateEnter()
    {
        Failed = false;
        _running = null;

        // A missing or broken Primary counts as failed.
        if (IsUsable(_primary))
        {
            _primary.OnStateEnter();
            if (_primary is not IFallibleAction { Failed: true })
            {
                _running = _primary;
                return;
            }
        }

        if (!IsUsable(_fallback))
        {
            Failed = true;
            return;
        }

        _fallback.OnStateEnter();
        _running = _fallback;
        Failed = _fallback is IFallibleAction { Failed: true };
    }

    public override void OnUpdate()
    {
        _running?.OnUpdate();
    }

    public override void OnFixedUpdate()
    {
        _running?.OnFixedUpdate();
    }

    public override void OnStateExit()
    {
        _running?.OnStateExit();
        _running = null;
    }

    private static bool IsUsable(StateAction action)
    {
        return action != null && !action.IsDisabled;
    }
}
