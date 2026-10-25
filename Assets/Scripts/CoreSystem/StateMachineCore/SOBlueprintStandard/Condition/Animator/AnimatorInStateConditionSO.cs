using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// True while the Animator is in the named state, however far it is. For "has it finished",
// use Animation Finished (AnimationFinishedConditionSO).
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Animator))]
[CreateAssetMenu(
    fileName = "NewAnimatorInState_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Animator/In State")]
public class AnimatorInStateConditionSO : StateConditionSO
{
    [Tooltip("The Animator state's name, e.g. \"Rest\" (or its full path, \"Base Layer.Rest\").")]
    [SerializeField] private string _stateName;
    [SerializeField] private int _layerIndex;
    [Tooltip("Also true while the Animator is fading into it. Off: only once it's the current state " +
             "(during a fade, the current state is still the one being left).")]
    [SerializeField] private bool _includeFadingIn = true;

    protected override Condition OnCreateCondition()
    {
        return new AnimatorInStateCondition(_stateName, _layerIndex, _includeFadingIn);
    }
}

public class AnimatorInStateCondition : Condition
{
    private readonly int _stateHash;
    private readonly int _layerIndex;
    private readonly bool _includeFadingIn;
    private AnchorReference<Animator> _animator;

    public AnimatorInStateCondition(string stateName, int layerIndex, bool includeFadingIn)
    {
        _stateHash = Animator.StringToHash(stateName ?? string.Empty);
        _layerIndex = layerIndex;
        _includeFadingIn = includeFadingIn;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _animator = new AnchorReference<Animator>(stateMachine, this);
    }

    protected override bool Statement()
    {
        if (!_animator.TryGet(out Animator animator) ||
            _layerIndex < 0 || _layerIndex >= animator.layerCount)
        {
            return false;
        }

        if (IsThisState(animator.GetCurrentAnimatorStateInfo(_layerIndex)))
        {
            return true;
        }

        return _includeFadingIn &&
               animator.IsInTransition(_layerIndex) &&
               IsThisState(animator.GetNextAnimatorStateInfo(_layerIndex));
    }

    private bool IsThisState(AnimatorStateInfo info)
    {
        return info.shortNameHash == _stateHash || info.fullPathHash == _stateHash;
    }
}
