using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Plays the FadeObject on the target when the state starts. The fade keeps running after the state
// ends, so a fade-out state can leave before it finishes.
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(FadeObject))]
[CreateAssetMenu(
    fileName = "NewPlayFade_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Fade/Play Fade")]
public class PlayFadeActionSO : StateActionSO
{
    [Tooltip("Seconds. 0 uses the Fade Duration already set on the FadeObject.")]
    [Min(0f)]
    [SerializeField] private float fadeDuration;

    [Tooltip("Off: starts from the value it is at now.")]
    [SerializeField] private bool useStartFade;

    [Range(0f, 1f)]
    [SerializeField] private float startFade;

    [Range(0f, 1f)]
    [SerializeField] private float endFade = 1f;

    [Tooltip("Raised when the fade finishes. Replaces the channel set on the FadeObject, also when empty.")]
    [SerializeField] private VoidEventChannelSO onFinishFade;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new PlayFadeAction(
            fadeDuration,
            useStartFade,
            startFade,
            endFade,
            onFinishFade);
    }
}

public class PlayFadeAction : StateAction
{
    private readonly float fadeDuration;
    private readonly bool useStartFade;
    private readonly float startFade;
    private readonly float endFade;
    private readonly VoidEventChannelSO onFinishFade;
    private AnchorReference<FadeObject> fadeObjectRef;

    public PlayFadeAction(
        float fadeDuration,
        bool useStartFade,
        float startFade,
        float endFade,
        VoidEventChannelSO onFinishFade)
    {
        this.fadeDuration = fadeDuration;
        this.useStartFade = useStartFade;
        this.startFade = startFade;
        this.endFade = endFade;
        this.onFinishFade = onFinishFade;
    }

    public override void Awake(StateMachine stateMachine)
    {
        fadeObjectRef = new AnchorReference<FadeObject>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!fadeObjectRef.TryGet(out FadeObject fadeObject)) return;

        if (useStartFade)
        {
            fadeObject.PlayFade(fadeDuration, startFade, endFade, onFinishFade);
        }
        else
        {
            fadeObject.PlayFade(fadeDuration, endFade, onFinishFade);
        }
    }

    public override void OnUpdate() { }
}
