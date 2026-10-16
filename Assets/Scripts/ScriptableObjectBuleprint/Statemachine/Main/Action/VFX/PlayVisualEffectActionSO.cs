using UnityEngine;
using UnityEngine.VFX;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// The VisualEffect is usually on a child: set Self Anchor to that child's AnchorID (and list the child
// in the owner's Provider). With no Self Anchor it looks on the owner itself. No RequiresOwnerComponent,
// since that would check the owner, not the child.
[UseSelfAnchor]
[CreateAssetMenu(
    fileName = "PlayVisualEffect_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/VFX/Play Visual Effect")]
public class PlayVisualEffectActionSO : StateActionSO
{
    [Tooltip("Empty: Play(). Otherwise sends this event to the VFX Graph instead (e.g. \"OnBurst\").")]
    [SerializeField] private string _eventName = "";

    [Tooltip("Start from nothing on enter (Reinit), so particles left from last time are cleared.")]
    [SerializeField] private bool _reinitOnEnter = false;

    [Tooltip("Stop when the state is left. On for looping effects (an aura), off for one-shots (a burst).")]
    [SerializeField] private bool _stopOnStateExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new PlayVisualEffectAction(_eventName, _reinitOnEnter, _stopOnStateExit);
    }
}

public class PlayVisualEffectAction : StateAction
{
    private readonly string _eventName;
    private readonly bool _reinitOnEnter;
    private readonly bool _stopOnStateExit;
    private AnchorReference<VisualEffect> _vfx;

    public PlayVisualEffectAction(string eventName, bool reinitOnEnter, bool stopOnStateExit)
    {
        _eventName = eventName;
        _reinitOnEnter = reinitOnEnter;
        _stopOnStateExit = stopOnStateExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _vfx = new AnchorReference<VisualEffect>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_vfx.TryGet(out VisualEffect vfx)) return;

        if (_reinitOnEnter)
            vfx.Reinit();

        if (string.IsNullOrEmpty(_eventName))
            vfx.Play();
        else
            vfx.SendEvent(_eventName);
    }

    public override void OnStateExit()
    {
        if (_stopOnStateExit && _vfx.TryGet(out VisualEffect vfx))
            vfx.Stop();
    }

    public override void OnUpdate() { }
}
