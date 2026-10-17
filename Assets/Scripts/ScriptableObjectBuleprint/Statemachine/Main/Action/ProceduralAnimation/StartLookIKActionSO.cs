using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(LookAtIKController))]
[CreateAssetMenu(
    fileName = "StartLookIK_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Procedural Animation/Head IK/Start Look IK")]
public class StartLookIKActionSO : StateActionSO
{
    [Tooltip("Required. What to look at. Self Anchor is the one doing the looking (empty = Owner).")]
    [SerializeField] private AnchorIDSO _targetAnchor;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new StartLookIKAction(_targetAnchor);
    }
}

public class StartLookIKAction : StateAction
{
    private readonly AnchorIDSO _targetAnchor;
    private AnchorReference<LookAtIKController> _lookAtIK;
    private AnchorReference<Transform> _target;

    public StartLookIKAction(AnchorIDSO targetAnchor)
    {
        _targetAnchor = targetAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _lookAtIK = new AnchorReference<LookAtIKController>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    public override void OnStateEnter()
    {
        if (_targetAnchor == null)
        {
            Debug.LogError("StartLookIKAction has no AnchorIDSO assigned.");
            return;
        }

        if (!_lookAtIK.TryGet(out LookAtIKController lookAtIK) ||
            !_target.TryGet(out Transform target))
        {
            return;
        }

        lookAtIK.SetLookTarget(target);
        lookAtIK.SetIKEnabled(true);
    }

    public override void OnStateExit()
    {
        if (!_lookAtIK.TryGet(out LookAtIKController lookAtIK)) return;
        lookAtIK.SetIKEnabled(false);
        lookAtIK.SetLookTarget(null);
    }

    public override void OnUpdate() { }
}
