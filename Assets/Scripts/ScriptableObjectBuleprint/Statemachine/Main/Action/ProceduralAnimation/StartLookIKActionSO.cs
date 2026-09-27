using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(LookAtIKController))]
[CreateAssetMenu(
    fileName = "StartLookIK_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Procedural Animation/Head IK/Start Look IK")]
public class StartLookIKActionSO : StateActionSO
{
    [SerializeField] private GameObjectAnchor _targetAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new StartLookIKAction(_targetAnchor);
    }
}

public class StartLookIKAction : StateAction
{
    private readonly GameObjectAnchor _targetAnchor;
    private LookAtIKController _lookAtIK;

    public StartLookIKAction(GameObjectAnchor targetAnchor)
    {
        _targetAnchor = targetAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _lookAtIK, this);
    }

    public override void OnStateEnter()
    {
        if (_targetAnchor == null)
        {
            Debug.LogError("StartLookIKAction has no GameObjectAnchor assigned.");
            return;
        }

        if (!_targetAnchor.IsSet || _targetAnchor.Value == null)
        {
            Debug.LogWarning("StartLookIKAction target anchor is not set.");
            return;
        }

        _lookAtIK.SetLookTarget(_targetAnchor.Value.transform);
        _lookAtIK.SetIKEnabled(true);
    }

    public override void OnStateExit()
    {
        _lookAtIK.SetIKEnabled(false);
        _lookAtIK.SetLookTarget(null);
    }

    public override void OnUpdate() { }
}
