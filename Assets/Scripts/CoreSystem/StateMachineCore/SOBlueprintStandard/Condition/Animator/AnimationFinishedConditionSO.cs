using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Animator))]
[CreateAssetMenu(
    fileName = "NewAnimFin_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Animator/Animation Finished")]
public class AnimationFinishedConditionSO : StateConditionSO
{
    [Header("Leave empty to auto-detect whichever state the Animator is currently playing")]
    [SerializeField] private string stateName;
    [SerializeField] private int layerIndex;
    [SerializeField, Range(0f, 1f)] private float finishTime = 1f;
    [SerializeField, Min(0f), Exposed] private float extraSeconds = 0f;

    public float ExtraSeconds
    {
        get => extraSeconds;
        set
        {
            if (value < 0f)
            {
                Debug.LogWarning("Extra seconds cannot be negative. Setting to 0.");
                extraSeconds = 0f;
                return;
            }
            extraSeconds = value;
        }
    }

    protected override Condition OnCreateCondition()
    {
        return new AnimationFinishedCondition(
            stateName,
            layerIndex,
            finishTime,
            extraSeconds);
    }
}

public class AnimationFinishedCondition : Condition
{
    private readonly string stateName;
    private readonly int layerIndex;
    private readonly float finishTime;
    private readonly float extraSeconds;
    private AnchorReference<Animator> animatorRef;
    // When the animation was first seen finished, or null while it isn't.
    // A timestamp, not a per-frame sum: Statement() isn't called every frame
    // (transitions stop at the first failing condition), so summing deltaTime there runs slow.
    private float? finishedSince;
    private int? capturedStateHash;

    public AnimationFinishedCondition(
        string stateName,
        int layerIndex,
        float finishTime,
        float extraSeconds)
    {
        this.stateName = stateName;
        this.layerIndex = layerIndex;
        this.finishTime = finishTime;
        this.extraSeconds = extraSeconds;
    }

    public override void Awake(StateMachine stateMachine)
    {
        animatorRef = new AnchorReference<Animator>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        finishedSince = null;

        // Force a fresh capture next Statement() check instead of comparing
        // against whatever this condition happened to watch the last time it
        // ran (e.g. a previous pass through this state, or - for conditions
        // used on an Any State transition, which never gets OnStateEnter
        // calls at all - the very first frame the game ever ran).
        capturedStateHash = null;
    }

        // Watches the Animator every frame (see Condition.OnTick), so the moment the animation
    // finishes is recorded on time, even on frames when Statement() is skipped.
    protected override void OnTick()
    {
        if (!animatorRef.TryGet(out Animator animator)) return;
        if (layerIndex < 0 || layerIndex >= animator.layerCount) return;
        if (animator.IsInTransition(layerIndex)) return;

        bool autoDetectState = string.IsNullOrWhiteSpace(stateName);

        AnimatorStateInfo stateInfo =
            animator.GetCurrentAnimatorStateInfo(layerIndex);

        bool isWatchedState;

        if (autoDetectState)
        {
            int currentHash = stateInfo.fullPathHash;

            if (capturedStateHash != currentHash)
            {
                // Either the first look ever, or the Animator has moved on
                // to a different state since we last looked (a new clip
                // started, or Play()/CrossFade() only just took effect).
                // (Re)lock onto whatever is playing now and wait for the
                // next frame before trusting its normalizedTime.
                capturedStateHash = currentHash;
                finishedSince = null;
                return;
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
            finishedSince = null;
            return;
        }

        finishedSince ??= Time.time;
    }

    protected override bool Statement()
    {
        return finishedSince.HasValue &&
               Time.time - finishedSince.Value >= extraSeconds;
    }
}