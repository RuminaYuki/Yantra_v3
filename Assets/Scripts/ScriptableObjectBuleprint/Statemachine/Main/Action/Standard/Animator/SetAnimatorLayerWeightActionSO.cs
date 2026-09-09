using System.Collections;
using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewSetAnimatorLayerWeightAction",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Animator/Set Layer Weight")]
public class SetAnimatorLayerWeightActionSO : StateActionSO
{
    [SerializeField] private string _layerName;
    [SerializeField, Range(0f, 1f)] private float _enterWeight = 1f;
    [SerializeField, Min(0f)] private float _enterDuration = 0.2f;

    [Header("Exit State")]
    [SerializeField] private bool _setWeightOnExit;
    [SerializeField, Range(0f, 1f)] private float _exitWeight;
    [SerializeField, Min(0f)] private float _exitDuration = 0.2f;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetAnimatorLayerWeightAction(
            _layerName,
            _enterWeight,
            _enterDuration,
            _setWeightOnExit,
            _exitWeight,
            _exitDuration);
    }
}

public class SetAnimatorLayerWeightAction : StateAction
{
    private readonly string _layerName;
    private readonly float _enterWeight;
    private readonly float _enterDuration;
    private readonly bool _setWeightOnExit;
    private readonly float _exitWeight;
    private readonly float _exitDuration;

    private Animator _animator;
    private StateMachineController _coroutineRunner;
    private int _layerIndex = -1;
    private Coroutine _weightCoroutine;

    public SetAnimatorLayerWeightAction(
        string layerName,
        float enterWeight,
        float enterDuration,
        bool setWeightOnExit,
        float exitWeight,
        float exitDuration)
    {
        _layerName = layerName;
        _enterWeight = enterWeight;
        _enterDuration = enterDuration;
        _setWeightOnExit = setWeightOnExit;
        _exitWeight = exitWeight;
        _exitDuration = exitDuration;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _animator = stateMachine.GetComponent<Animator>();
        _coroutineRunner = stateMachine.GetComponent<StateMachineController>();

        if (_animator == null || _coroutineRunner == null)
        {
            Debug.LogError(
                $"{nameof(SetAnimatorLayerWeightAction)} requires Animator and " +
                $"StateMachineController on " +
                $"{stateMachine.Owner.name}.",
                stateMachine.Owner);
            return;
        }

        _layerIndex = _animator.GetLayerIndex(_layerName);

        if (_layerIndex < 0)
        {
            Debug.LogError(
                $"Animator layer '{_layerName}' does not exist on " +
                $"{stateMachine.Owner.name}.",
                stateMachine.Owner);
        }
    }

    public override void OnStateEnter()
    {
        StartWeightLerp(_enterWeight, _enterDuration);
    }

    public override void OnUpdate()
    {
    }

    public override void OnStateExit()
    {
        if (_setWeightOnExit)
        {
            StartWeightLerp(_exitWeight, _exitDuration);
        }
    }

    private void StartWeightLerp(float targetWeight, float duration)
    {
        if (_animator == null || _coroutineRunner == null || _layerIndex < 0)
        {
            return;
        }

        if (_weightCoroutine != null)
        {
            _coroutineRunner.StopCoroutine(_weightCoroutine);
            _weightCoroutine = null;
        }

        if (duration <= 0f)
        {
            _animator.SetLayerWeight(_layerIndex, targetWeight);
            return;
        }

        _weightCoroutine = _coroutineRunner.StartCoroutine(
            LerpWeight(targetWeight, duration));
    }

    private IEnumerator LerpWeight(float targetWeight, float duration)
    {
        float startWeight = _animator.GetLayerWeight(_layerIndex);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            _animator.SetLayerWeight(
                _layerIndex,
                Mathf.Lerp(startWeight, targetWeight, t));

            yield return null;
        }

        _animator.SetLayerWeight(_layerIndex, targetWeight);
        _weightCoroutine = null;
    }
}
