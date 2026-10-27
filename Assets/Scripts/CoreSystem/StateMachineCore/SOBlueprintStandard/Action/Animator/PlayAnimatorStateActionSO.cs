using UnityEngine;
using UnityEngine.Serialization;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Animator))]
[CreateAssetMenu(
    fileName = "NewPlayAnimState_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Animator/PlayAnimatorState")]
public class PlayAnimatorStateActionSO : StateActionSO
{
    [Header("Enter State")]
    [SerializeField] private string _stateName;
    [SerializeField] private int _layerIndex;
    [SerializeField] private float _transitionDuration = 0.25f;
    [Tooltip("Seconds into the state to start from.")]
    [SerializeField, Min(0f), FormerlySerializedAs("_normalizedStartTime")] private float _startTimeOffset;
    [Tooltip("If the Animator is playing (or fading into) this state when entering, e.g. the previous " +
             "state's Exit State, play after it finishes. Empty = play right away.")]
    [SerializeField] private string _waitForStateName;

    [Header("Exit State")]
    [SerializeField] private string _exitStateName;
    [SerializeField] private float _exitTransitionDuration = 0.25f;
    [Tooltip("Seconds into the exit state to start from.")]
    [SerializeField, Min(0f), FormerlySerializedAs("_exitNormalizedStartTime")] private float _exitStartTimeOffset;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new PlayAnimatorStateAction(
            _stateName,
            _exitStateName,
            _waitForStateName,
            _layerIndex,
            _transitionDuration,
            _startTimeOffset,
            _exitTransitionDuration,
            _exitStartTimeOffset);
    }
}

public class PlayAnimatorStateAction : StateAction
{
    private readonly int _stateHash;
    private readonly int _exitStateHash;
    private readonly int _waitForStateHash;
    private readonly int _layerIndex;
    private readonly float _transitionDuration;
    private readonly float _startTimeOffset;
    private readonly float _exitTransitionDuration;
    private readonly float _exitStartTimeOffset;

    private AnchorReference<Animator> _animator;

    private readonly bool _hasExitState;
    private readonly bool _hasWaitForState;

    // Set while the enter state waits for _waitForStateHash to finish.
    private bool _isWaiting;
    private int _enterFrame;

    public PlayAnimatorStateAction(
        string stateName,
        string exitStateName,
        string waitForStateName,
        int layerIndex,
        float transitionDuration,
        float startTimeOffset,
        float exitTransitionDuration,
        float exitStartTimeOffset)
    {
        _hasExitState = !string.IsNullOrWhiteSpace(exitStateName);
        _exitStateHash = _hasExitState ? Animator.StringToHash(exitStateName) : 0;
        _hasWaitForState = !string.IsNullOrWhiteSpace(waitForStateName);
        _waitForStateHash = _hasWaitForState ? Animator.StringToHash(waitForStateName) : 0;

        _stateHash = Animator.StringToHash(stateName);
        _layerIndex = layerIndex;
        _transitionDuration = transitionDuration;
        _startTimeOffset = startTimeOffset;
        _exitTransitionDuration = exitTransitionDuration;
        _exitStartTimeOffset = exitStartTimeOffset;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _animator = new AnchorReference<Animator>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_hasWaitForState)
        {
            PlayState(_stateHash, _transitionDuration, _startTimeOffset);
            return;
        }

        // The previous state's Exit State CrossFade only shows in the Animator after its next update,
        // so it can't be seen yet: look from the next frame on (OnUpdate).
        _isWaiting = true;
        _enterFrame = UnityEngine.Time.frameCount;
    }

    public override void OnUpdate()
    {
        if (!_isWaiting || UnityEngine.Time.frameCount == _enterFrame)
        {
            return;
        }

        if (!_animator.TryGet(out Animator animator) || IsPlayingWaitForState(animator))
        {
            return;
        }

        _isWaiting = false;
        PlayState(_stateHash, _transitionDuration, _startTimeOffset);
    }

    public override void OnStateExit()
    {
        // Left before the wait ended: don't play this state's animation in the next one.
        _isWaiting = false;

        if (!_hasExitState ||
            !_animator.TryGet(out Animator animator) ||
            !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null)
        {
            return;
        }

        PlayState(
            _exitStateHash,
            _exitTransitionDuration,
            _exitStartTimeOffset);
    }

    // Fading into it, or in it and not finished. Fading out of it (cut by another CrossFade, or the
    // Animator's own transition) counts as done, so nothing waits forever.
    private bool IsPlayingWaitForState(Animator animator)
    {
        if (animator.IsInTransition(_layerIndex))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(_layerIndex);
            return IsWaitForState(next);
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(_layerIndex);
        return IsWaitForState(current) && current.normalizedTime < 1f;
    }

    private bool IsWaitForState(AnimatorStateInfo info)
    {
        return info.shortNameHash == _waitForStateHash || info.fullPathHash == _waitForStateHash;
    }

    private void PlayState(int stateHash, float transitionDuration, float startTimeOffset)
    {
        if (!_animator.TryGet(out Animator animator))
            return;

        if (!animator.HasState(_layerIndex, stateHash))
        {
            Debug.LogWarning(
                $"Animator state does not exist: hash {stateHash}, " +
                $"layer {_layerIndex}.",
                animator);

            return;
        }

        animator.CrossFadeInFixedTime(
            stateHash,
            transitionDuration,
            _layerIndex,
            startTimeOffset);
    }
}
