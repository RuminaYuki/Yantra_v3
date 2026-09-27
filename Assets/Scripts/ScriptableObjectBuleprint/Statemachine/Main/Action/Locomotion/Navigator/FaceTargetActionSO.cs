using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "FaceTarget_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Face Target")]
public class FaceTargetActionSO : StateActionSO
{
    [SerializeField] private GameObjectAnchor _targetAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new FaceTargetAction(_targetAnchor);
    }
}

public class FaceTargetAction : StateAction
{
    private readonly GameObjectAnchor _targetAnchor;
    private BaseLocomotion _locomotion;
    private Transform _owner;

    public FaceTargetAction(GameObjectAnchor targetAnchor)
    {
        _targetAnchor = targetAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner.transform;
        stateMachine.TryGetRequired(out _locomotion, this);

        if (_targetAnchor == null)
            Debug.LogError("FaceTargetAction has no target GameObjectAnchor assigned.");
    }

    public override void OnUpdate()
    {
        if (_owner == null)
            return;

        if (_targetAnchor == null || !_targetAnchor.IsSet || _targetAnchor.Value == null)
            return;

        Vector3 direction = _targetAnchor.Value.transform.position - _owner.position;
        _locomotion.SetFacingDirection(direction);
    }
}
