using System.Collections;
using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Animator))]
[RequiresOwnerComponent(typeof(StateMachineController))]
[CreateAssetMenu(
    fileName = "NewSetAnimatorLayerWeightAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Animator/Set Layer Weight")]
public class SetAnimatorLayerWeightActionSO : StateActionSO
{
    [SerializeField] private string _layerName;
    [SerializeField, Range(0f, 1f)] private float _enterWeight = 1f;
    [SerializeField, Min(0f)] private float _enterDuration = 0.2f;

    [Header("Exit State")]
    [SerializeField] private bool _setWeightOnExit;
    [SerializeField, Range(0f, 1f)] private float _exitWeight;
    [SerializeField, Min(0f)] private float _exitDuration = 0.2f;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
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

    // Animator follows the Self Anchor; the coroutine always runs on this state machine's own controller.
    private AnchorReference<Animator> _animator;
    private AnchorReference<StateMachineController> _coroutineRunner;
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
        _animator = new AnchorReference<Animator>(stateMachine, this);
        _coroutineRunner = new AnchorReference<StateMachineController>(stateMachine, null, this);
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
        if (!_animator.TryGet(out Animator animator) ||
            !_coroutineRunner.TryGet(out StateMachineController runner))
        {
            return;
        }

        // Looked up each time: the anchored animator can change while playing.
        int layerIndex = animator.GetLayerIndex(_layerName);
        if (layerIndex < 0)
        {
            Debug.LogError($"Animator layer '{_layerName}' does not exist on {animator.name}.", animator);
            return;
        }

        if (_weightCoroutine != null)
        {
            runner.StopCoroutine(_weightCoroutine);
            _weightCoroutine = null;
        }

        if (duration <= 0f)
        {
            animator.SetLayerWeight(layerIndex, targetWeight);
            return;
        }

        _weightCoroutine = runner.StartCoroutine(
            LerpWeight(animator, layerIndex, targetWeight, duration));
    }

    private IEnumerator LerpWeight(Animator animator, int layerIndex, float targetWeight, float duration)
    {
        float startWeight = animator.GetLayerWeight(layerIndex);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            if (animator == null) yield break;   // destroyed mid-lerp

            animator.SetLayerWeight(
                layerIndex,
                Mathf.Lerp(startWeight, targetWeight, t));

            yield return null;
        }

        if (animator != null)
            animator.SetLayerWeight(layerIndex, targetWeight);
        _weightCoroutine = null;
    }
}
