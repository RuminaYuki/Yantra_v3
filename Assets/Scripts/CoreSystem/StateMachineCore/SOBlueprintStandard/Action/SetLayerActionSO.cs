using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetLayer_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Set Layer")]
public class SetLayerActionSO : StateActionSO
{
    [Tooltip("Pick exactly one layer - the owner's GameObject switches to it while this state is active.")]
    [SerializeField] private LayerMask _layer;
    [Tooltip("Restore the owner's original layer when this state is exited.")]
    [SerializeField] private bool _restoreOnExit = true;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetLayerAction(_layer, _restoreOnExit);
    }
}

public class SetLayerAction : StateAction
{
    private readonly int _layer;
    private readonly bool _restoreOnExit;
    private GameObject _owner;
    private int _originalLayer;

    public SetLayerAction(LayerMask layer, bool restoreOnExit)
    {
        _layer = LayerMaskToLayer(layer);
        _restoreOnExit = restoreOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner;
    }

    public override void OnStateEnter()
    {
        if (_owner == null) return;

        _originalLayer = _owner.layer;
        _owner.layer = _layer;
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (_owner == null || !_restoreOnExit) return;

        _owner.layer = _originalLayer;
    }

    private static int LayerMaskToLayer(LayerMask mask)
    {
        int value = mask.value;
        for (int i = 0; i < 32; i++)
        {
            if ((value & (1 << i)) != 0)
                return i;
        }
        return 0;
    }
}
