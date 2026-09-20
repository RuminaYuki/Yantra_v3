using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "FaceTarget_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Face Target")]
public class FaceTargetActionSO : StateActionSO
{
    [SerializeField] private TransformAnchor _targetAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new FaceTargetAction(_targetAnchor);
    }
}

public class FaceTargetAction : StateAction
{
    private readonly TransformAnchor _targetAnchor;
    private BaseLocomotion _locomotion;
    private Transform _owner;

    public FaceTargetAction(TransformAnchor targetAnchor)
    {
        _targetAnchor = targetAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner.transform;
        _locomotion = stateMachine.GetComponent<BaseLocomotion>();

        if (_locomotion == null)
            Debug.LogError("FaceTargetAction cannot find BaseLocomotion.");

        if (_targetAnchor == null)
            Debug.LogError("FaceTargetAction has no target TransformAnchor assigned.");
    }

    public override void OnUpdate()
    {
        if (_locomotion == null || _owner == null)
            return;

        if (_targetAnchor == null || !_targetAnchor.IsSet || _targetAnchor.Value == null)
            return;

        Vector3 direction = _targetAnchor.Value.position - _owner.position;
        _locomotion.SetFacingDirection(direction);
    }
}
