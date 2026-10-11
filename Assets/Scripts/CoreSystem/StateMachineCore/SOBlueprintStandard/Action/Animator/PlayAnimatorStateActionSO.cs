using UnityEngine;
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
    [SerializeField, Range(0f, 1f)] private float _normalizedStartTime;

    [Header("Exit State")]
    [SerializeField] private string _exitStateName;
    [SerializeField] private float _exitTransitionDuration = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _exitNormalizedStartTime;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new PlayAnimatorStateAction(
            _stateName,
            _exitStateName,
            _layerIndex,
            _transitionDuration,
            _normalizedStartTime,
            _exitTransitionDuration,
            _exitNormalizedStartTime);
    }
}

public class PlayAnimatorStateAction : StateAction
{
    private readonly int _stateHash;
    private readonly int _exitStateHash;
    private readonly int _layerIndex;
    private readonly float _transitionDuration;
    private readonly float _normalizedStartTime;
    private readonly float _exitTransitionDuration;
    private readonly float _exitNormalizedStartTime;

    private AnchorReference<Animator> _animator;

    private readonly bool _hasExitState;

    public PlayAnimatorStateAction(
        string stateName,
        string exitStateName,
        int layerIndex,
        float transitionDuration,
        float normalizedStartTime,
        float exitTransitionDuration,
        float exitNormalizedStartTime)
    {
        _hasExitState = !string.IsNullOrWhiteSpace(exitStateName);
        _exitStateHash = _hasExitState ? Animator.StringToHash(exitStateName) : 0;

        _stateHash = Animator.StringToHash(stateName);
        _exitStateHash = Animator.StringToHash(exitStateName);
        _layerIndex = layerIndex;
        _transitionDuration = transitionDuration;
        _normalizedStartTime = normalizedStartTime;
        _exitTransitionDuration = exitTransitionDuration;
        _exitNormalizedStartTime = exitNormalizedStartTime;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _animator = new AnchorReference<Animator>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        PlayState(
            _stateHash,
            _transitionDuration,
            _normalizedStartTime);
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
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
            _exitNormalizedStartTime);
    }

    private void PlayState(int stateHash, float transitionDuration, float normalizedStartTime)
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
            normalizedStartTime);
    }
}
