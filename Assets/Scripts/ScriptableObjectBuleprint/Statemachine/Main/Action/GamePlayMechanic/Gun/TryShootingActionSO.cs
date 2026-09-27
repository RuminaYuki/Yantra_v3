using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "TryShooting_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Gun/Try Shooting")]
public class TryShootingActionSO : StateActionSO
{
    [SerializeField] private GameObjectAnchor _gunAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new TryShootingAction(_gunAnchor);
    }
}

public class TryShootingAction : StateAction
{
    private readonly GameObjectAnchor _gunAnchor;
    private GunController _gunController;

    public TryShootingAction(GameObjectAnchor gunAnchor)
    {
        _gunAnchor = gunAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
         _gunAnchor.TryGetComponentOrOwner(stateMachine, out _gunController, this);
    }

    public override void OnStateEnter()
    {
        _gunController?.TryShooting();
    }

    public override void OnUpdate() { }
}
