using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "StartLookIK_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Procedural Animation/Head IK/Start Look IK")]
public class StartLookIKActionSO : StateActionSO
{
    [SerializeField] private TransformAnchor _targetAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new StartLookIKAction(_targetAnchor);
    }
}

public class StartLookIKAction : StateAction
{
    private readonly TransformAnchor _targetAnchor;
    private LookAtIKController _lookAtIK;

    public StartLookIKAction(TransformAnchor targetAnchor)
    {
        _targetAnchor = targetAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _lookAtIK = stateMachine.GetComponent<LookAtIKController>();

        if (_lookAtIK == null)
            Debug.LogError("StartLookIKAction cannot find LookAtIKController.");
    }

    public override void OnStateEnter()
    {
        if (_lookAtIK == null)
            return;

        if (_targetAnchor == null)
        {
            Debug.LogError("StartLookIKAction has no TransformAnchor assigned.");
            return;
        }

        if (!_targetAnchor.IsSet || _targetAnchor.Value == null)
        {
            Debug.LogWarning("StartLookIKAction target anchor is not set.");
            return;
        }

        _lookAtIK.SetLookTarget(_targetAnchor.Value);
        _lookAtIK.SetIKEnabled(true);
    }

    public override void OnStateExit()
    {
        if (_lookAtIK == null)
            return;

        _lookAtIK.SetIKEnabled(false);
        _lookAtIK.SetLookTarget(null);
    }

    public override void OnUpdate() { }
}
