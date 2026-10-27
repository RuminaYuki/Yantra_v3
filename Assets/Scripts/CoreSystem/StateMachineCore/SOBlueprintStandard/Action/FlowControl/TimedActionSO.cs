using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Runs one action only during a window of the state: the action is entered when the window opens,
// updated while it's open, and exited when it closes or when the state is left early (so a hitbox
// can't be left on). Each window opens at most once per pass through the state.
// Self Anchor is only used in Anim Normalized mode, to find the Animator.
[UseSelfAnchor]
[CreateAssetMenu(
    fileName = "NewTimed_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/FlowControl/Timed")]
public class TimedActionSO : StateActionSO
{
    public enum TimeUnit
    {
        [Tooltip("Seconds since the state was entered. For game rules (i-frames for 2s) and states without a fixed clip.")]
        Seconds,

        [Tooltip("Progress through an Animator state (0 = start, 1 = end). Follows the clip when it gets longer, " +
                 "shorter or sped up. For timings tied to the motion (the swing, the throw).")]
        AnimNormalized
    }

    [SerializeField] private TimeUnit _unit = TimeUnit.Seconds;

    [Tooltip("When the action starts, in the chosen unit.")]
    [SerializeField, Min(0f), Exposed] private float _start;

    [Tooltip("When the action stops, in the chosen unit. 0 = keeps running until the state exits.")]
    [SerializeField, Min(0f), Exposed] private float _end;

    [Tooltip("The action to run during the window.")]
    [SerializeField] private StateActionSO _action;

    [Header("Anim Normalized only")]
    [Tooltip("The Animator state whose progress is read. Before it plays (e.g. during the fade in), the window stays shut.")]
    [SerializeField] private string _stateName;
    [SerializeField] private int _layerIndex;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        StateAction action = null;
        if (_action != null)
        {
            // Through the Resolver, like StateSO.CreateState, so Variant and character overrides reach it.
            StateActionSO resolved = stateMachine.Resolver != null ? stateMachine.Resolver.Resolve(_action) : _action;
            action = resolved.CreateAction(stateMachine);
        }

        return new TimedAction(action, _unit, _start, _end, _stateName, _layerIndex);
    }
}

public class TimedAction : StateAction
{
    private readonly StateAction _action;
    private readonly TimedActionSO.TimeUnit _unit;
    private readonly float _start;
    private readonly float _end;
    private readonly bool _hasEnd;
    private readonly int _stateHash;
    private readonly int _layerIndex;

    private AnchorReference<Animator> _animator;

    private float _enterTime;
    private int _enterFrame;
    // False: waiting for the window. True: inside it. Done: closed for the rest of this pass.
    private bool _isOpen;
    private bool _isDone;

    public TimedAction(
        StateAction action,
        TimedActionSO.TimeUnit unit,
        float start,
        float end,
        string stateName,
        int layerIndex)
    {
        _action = action;
        _unit = unit;
        _start = start;
        _end = end;
        _hasEnd = end > 0f;
        _stateHash = string.IsNullOrWhiteSpace(stateName) ? 0 : Animator.StringToHash(stateName);
        _layerIndex = layerIndex;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (_action == null)
        {
            Debug.LogError("[TimedAction] has no Action to run.", stateMachine.Owner);
            Disable();
            return;
        }

        if (_hasEnd && _end <= _start)
        {
            Debug.LogError($"[TimedAction] End ({_end}) must be after Start ({_start}), or 0 for no end.", stateMachine.Owner);
            Disable();
            return;
        }

        if (_unit == TimedActionSO.TimeUnit.AnimNormalized)
        {
            if (_stateHash == 0)
            {
                Debug.LogError("[TimedAction] Anim Normalized needs a State Name.", stateMachine.Owner);
                Disable();
                return;
            }

            _animator = new AnchorReference<Animator>(stateMachine, this);
        }

        // The state only knows about this wrapper, so pass Awake on.
        _action.Initialize(stateMachine);

        // A broken inner action makes this one broken too.
        if (_action.IsDisabled)
        {
            Disable();
        }
    }

    public override void OnStateEnter()
    {
        _enterTime = Time.Now;
        _enterFrame = UnityEngine.Time.frameCount;
        _isOpen = false;
        _isDone = false;

        // A window starting at 0 opens on the same frame as the state.
        UpdateWindow();
    }

    public override void OnUpdate()
    {
        UpdateWindow();

        if (_isOpen)
        {
            _action.OnUpdate();
        }
    }

    public override void OnFixedUpdate()
    {
        if (_isOpen)
        {
            _action.OnFixedUpdate();
        }
    }

    public override void OnStateExit()
    {
        // Left mid-window: close it so the action can clean up.
        Close();
    }

    private void UpdateWindow()
    {
        if (_isDone || !TryGetTime(out float time))
        {
            return;
        }

        if (!_isOpen && time >= _start)
        {
            _isOpen = true;
            _action.OnStateEnter();
        }

        // Checked straight after opening too, so a window shorter than a frame still runs enter + exit.
        if (_isOpen && _hasEnd && time >= _end)
        {
            Close();
        }
    }

    private void Close()
    {
        if (_isOpen)
        {
            _action.OnStateExit();
        }

        _isOpen = false;
        _isDone = true;
    }

    // False while the time can't be read yet (Animator missing, or not in the state); the window holds.
    private bool TryGetTime(out float time)
    {
        if (_unit == TimedActionSO.TimeUnit.Seconds)
        {
            time = Time.Now - _enterTime;
            return true;
        }

        time = 0f;

        // A CrossFade made on entering only shows in the Animator after its next update; until then it
        // still reports the previous clip, which could be this same state near its end.
        if (UnityEngine.Time.frameCount == _enterFrame)
        {
            return false;
        }

        if (!_animator.TryGet(out Animator animator))
        {
            return false;
        }

        if (_layerIndex < 0 || _layerIndex >= animator.layerCount)
        {
            return false;
        }

        // While fading in, the target is the Animator's "next" state, not its current one.
        AnimatorStateInfo info = animator.IsInTransition(_layerIndex)
            ? animator.GetNextAnimatorStateInfo(_layerIndex)
            : animator.GetCurrentAnimatorStateInfo(_layerIndex);

        if (info.shortNameHash != _stateHash && info.fullPathHash != _stateHash)
        {
            return false;
        }

        // Not wrapped to 0-1: a looping clip passes 1 and keeps going, so the window doesn't reopen each loop.
        time = info.normalizedTime;
        return true;
    }
}
