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
    [Header("Leave empty to auto-detect whichever state the Animator is currently playing")]
    [SerializeField] private string stateName;
    [SerializeField] private int layerIndex;
    [SerializeField, Range(0f, 1f)] private float finishTime = 1f;
    [SerializeField, Min(0f)] private float extraSeconds = 0f;

    public override Condition CreateCondition()
    {
        return new AnimationFinishedCondition(
            animatorAnchor,
            stateName,
            layerIndex,
            finishTime,
            extraSeconds);
    }
}

public class AnimationFinishedCondition : Condition
{
    private readonly AnimatorAnchor animatorAnchor;
    private readonly string stateName;
    private readonly int layerIndex;
    private readonly float finishTime;
    private readonly float extraSeconds;
    private Animator animator;
    private float extraTimeElapsed;
    private int? capturedStateHash;

    public AnimationFinishedCondition(
        AnimatorAnchor animatorAnchor,
        string stateName,
        int layerIndex,
        float finishTime,
        float extraSeconds)
    {
        this.animatorAnchor = animatorAnchor;
        this.stateName = stateName;
        this.layerIndex = layerIndex;
        this.finishTime = finishTime;
        this.extraSeconds = extraSeconds;
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

    public override void OnStateEnter()
    {
        extraTimeElapsed = 0f;

        // Force a fresh capture next Statement() check instead of comparing
        // against whatever this condition happened to watch the last time it
        // ran (e.g. a previous pass through this state, or - for conditions
        // used on an Any State transition, which never gets OnStateEnter
        // calls at all - the very first frame the game ever ran).
        capturedStateHash = null;
    }

    protected override bool Statement()
    {
        if (animator == null) return false;
        if (layerIndex < 0 || layerIndex >= animator.layerCount) return false;
        if (animator.IsInTransition(layerIndex)) return false;

        bool autoDetectState = string.IsNullOrWhiteSpace(stateName);

        AnimatorStateInfo stateInfo =
            animator.GetCurrentAnimatorStateInfo(layerIndex);

        bool isWatchedState;

        if (autoDetectState)
        {
            int currentHash = stateInfo.fullPathHash;

            if (capturedStateHash != currentHash)
            {
                // Either the first check ever, or the Animator has moved on
                // to a different state since we last looked (a new clip
                // started, or Play()/CrossFade() only just took effect).
                // (Re)lock onto whatever is playing now and wait for the
                // next check before trusting its normalizedTime.
                capturedStateHash = currentHash;
                extraTimeElapsed = 0f;
                return false;
            }

            isWatchedState = true;
        }
        else
        {
            isWatchedState = stateInfo.IsName(stateName);
        }

        bool animationReachedFinishTime =
            isWatchedState &&
            stateInfo.normalizedTime >= finishTime;

        if (!animationReachedFinishTime)
        {
            extraTimeElapsed = 0f;
            return false;
        }

        if (extraSeconds <= 0f) return true;

        extraTimeElapsed += Time.deltaTime;
        return extraTimeElapsed >= extraSeconds;
    }
}