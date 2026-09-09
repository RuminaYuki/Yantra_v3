using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "PlayAnimatorState_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Standard/Animator/PlayAnimatorState")]
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

    [Header("If the TargetAnchor is set, Check target flag instead")]
    [SerializeField] private AnimatorAnchor targetAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new PlayAnimatorStateAction(
            _stateName,
            _exitStateName,
            _layerIndex,
            _transitionDuration,
            _normalizedStartTime,
            _exitTransitionDuration,
            _exitNormalizedStartTime,
            targetAnchor);
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

    private Animator _animator;
    private readonly AnimatorAnchor _targetAnchor;

    private readonly bool _hasExitState;

    public PlayAnimatorStateAction(
        string stateName,
        string exitStateName,
        int layerIndex,
        float transitionDuration,
        float normalizedStartTime,
        float exitTransitionDuration,
        float exitNormalizedStartTime,
        AnimatorAnchor targetAnchor = null)
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
        _targetAnchor = targetAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if(_targetAnchor != null)
        {
            _animator = _targetAnchor.Value;
        }
        else
        {
            _animator = stateMachine.GetComponent<Animator>();
        }

        if (_animator == null)
            Debug.LogError(
                "PlayAnimatorStateAction cannot find Animator.");
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
            _animator == null ||
            !_animator.isActiveAndEnabled ||
            _animator.runtimeAnimatorController == null)
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
        if (_animator == null)
            return;

        if (!_animator.HasState(_layerIndex, stateHash))
        {
            Debug.LogWarning(
                $"Animator state does not exist: hash {stateHash}, " +
                $"layer {_layerIndex}.",
                _animator);

            return;
        }

        _animator.CrossFadeInFixedTime(
            stateHash,
            transitionDuration,
            _layerIndex,
            normalizedStartTime);
    }
}
