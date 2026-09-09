using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewAnimationFinished_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/Animation Finished")]
public class AnimationFinishedConditionSO : StateConditionSO
{
    [Header("If AnimatorAnchor has Set It will Check Target Instead")]
    [SerializeField] private AnimatorAnchor animatorAnchor;
    [SerializeField] private string stateName;
    [SerializeField] private int layerIndex;
    [SerializeField, Range(0f, 1f)] private float finishTime = 1f;

    public override Condition CreateCondition()
    {
        return new AnimationFinishedCondition(
            animatorAnchor,
            stateName,
            layerIndex,
            finishTime);
    }
}

public class AnimationFinishedCondition : Condition
{
    private readonly AnimatorAnchor animatorAnchor;
    private readonly string stateName;
    private readonly int layerIndex;
    private readonly float finishTime;
    private Animator animator;

    public AnimationFinishedCondition(
        AnimatorAnchor animatorAnchor,
        string stateName,
        int layerIndex,
        float finishTime)
    {
        this.animatorAnchor = animatorAnchor;
        this.stateName = stateName;
        this.layerIndex = layerIndex;
        this.finishTime = finishTime;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (animatorAnchor == null)
            animator = stateMachine.GetComponent<Animator>();
        else if (animatorAnchor.IsSet)
            animator = animatorAnchor.Value.GetComponent<Animator>();

        if (animator == null)
            Debug.LogError("AnimationFinishedCondition cannot find Animator.");
    }

    protected override bool Statement()
    {
        if (animator == null) return false;
        if (string.IsNullOrWhiteSpace(stateName)) return false;
        if (layerIndex < 0 || layerIndex >= animator.layerCount) return false;
        if (animator.IsInTransition(layerIndex)) return false;

        AnimatorStateInfo stateInfo =
            animator.GetCurrentAnimatorStateInfo(layerIndex);

        return stateInfo.IsName(stateName) &&
               stateInfo.normalizedTime >= finishTime;
    }
}