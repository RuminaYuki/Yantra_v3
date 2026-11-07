using UnityEngine;
using UnityEngine.Splines;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Self Anchor ว่าง = ใช้ SplineInstantiate บน Owner / ใส่ Anchor = ใช้ตัวที่ Anchor ชี้ (เช่น อยู่ตัวลูก)
// PlayAnimatorState เวอร์ชันที่สั่ง Animator ของทุก instance ที่ SplineInstantiate สร้างไว้ (เช่น หนามทุกอัน)
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(SplineInstantiate))]
[CreateAssetMenu(
    fileName = "NewPlaySplineItemsAnimState_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Spline/Play Spline Items Animator State")]
public class PlaySplineItemsAnimatorStateActionSO : StateActionSO
{
    [Header("Enter State")]
    [SerializeField] private string _stateName;
    [SerializeField] private int _layerIndex;
    [SerializeField] private float _transitionDuration = 0.25f;
    [Tooltip("Seconds into the state to start from.")]
    [SerializeField, Min(0f)] private float _startTimeOffset;
    [Tooltip("Each item playing (or fading into) this state when entering, e.g. the previous " +
             "state's Exit State, plays after it finishes. Empty = play right away.")]
    [SerializeField] private string _waitForStateName;

    [Header("Exit State")]
    [SerializeField] private string _exitStateName;
    [SerializeField] private float _exitTransitionDuration = 0.25f;
    [Tooltip("Seconds into the exit state to start from.")]
    [SerializeField, Min(0f)] private float _exitStartTimeOffset;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new PlaySplineItemsAnimatorStateAction(
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

public class PlaySplineItemsAnimatorStateAction : StateAction
{
    private readonly int _stateHash;
    private readonly int _exitStateHash;
    private readonly int _waitForStateHash;
    private readonly int _layerIndex;
    private readonly float _transitionDuration;
    private readonly float _startTimeOffset;
    private readonly float _exitTransitionDuration;
    private readonly float _exitStartTimeOffset;

    private AnchorReference<SplineInstantiate> _splineInstantiate;

    private readonly bool _hasExitState;
    private readonly bool _hasWaitForState;

    // Items found on enter; _isWaiting[i] is set while item i waits for _waitForStateHash to finish.
    private Animator[] _animators;
    private bool[] _isWaiting;
    private int _waitingCount;
    private int _enterFrame;

    public PlaySplineItemsAnimatorStateAction(
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
        _splineInstantiate = new AnchorReference<SplineInstantiate>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        // Look the items up every enter - SplineInstantiate may have regenerated them since last time.
        _animators = FindItemAnimators();
        if (_animators == null) return;

        if (!_hasWaitForState)
        {
            foreach (Animator animator in _animators)
                PlayState(animator, _stateHash, _transitionDuration, _startTimeOffset);
            return;
        }

        // The previous state's Exit State CrossFade only shows in the Animator after its next update,
        // so it can't be seen yet: look from the next frame on (OnUpdate).
        _isWaiting = new bool[_animators.Length];
        for (int i = 0; i < _isWaiting.Length; i++)
            _isWaiting[i] = true;
        _waitingCount = _animators.Length;
        _enterFrame = UnityEngine.Time.frameCount;
    }

    public override void OnUpdate()
    {
        if (_waitingCount == 0 || UnityEngine.Time.frameCount == _enterFrame)
        {
            return;
        }

        // Each item starts on its own once its wait-for state finishes.
        for (int i = 0; i < _animators.Length; i++)
        {
            if (!_isWaiting[i]) continue;

            Animator animator = _animators[i];
            if (animator != null && IsPlayingWaitForState(animator)) continue;

            _isWaiting[i] = false;
            _waitingCount--;
            if (animator != null)
                PlayState(animator, _stateHash, _transitionDuration, _startTimeOffset);
        }
    }

    public override void OnStateExit()
    {
        // Left before the wait ended: don't play this state's animation in the next one.
        _waitingCount = 0;
        _isWaiting = null;

        if (!_hasExitState) return;

        // Look again - items may have been regenerated while in this state.
        Animator[] animators = FindItemAnimators();
        if (animators == null) return;

        foreach (Animator animator in animators)
        {
            if (!animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
                continue;

            PlayState(animator, _exitStateHash, _exitTransitionDuration, _exitStartTimeOffset);
        }
    }

    // Instances live under a hidden root child of SplineInstantiate - still found by GetComponentsInChildren.
    private Animator[] FindItemAnimators()
    {
        if (!_splineInstantiate.TryGet(out SplineInstantiate spline))
            return null;

        return spline.GetComponentsInChildren<Animator>(true);
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

    private void PlayState(Animator animator, int stateHash, float transitionDuration, float startTimeOffset)
    {
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
