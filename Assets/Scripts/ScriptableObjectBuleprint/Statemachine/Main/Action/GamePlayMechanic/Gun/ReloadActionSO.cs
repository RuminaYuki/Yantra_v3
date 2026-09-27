using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "Reload_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Gun/Reload")]
public class ReloadActionSO : StateActionSO
{
    [SerializeField] private GameObjectAnchor _gunAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new ReloadAction(_gunAnchor);
    }
}

public class ReloadAction : StateAction
{
    private readonly GameObjectAnchor _gunAnchor;
    private GunController _gunController;

    public ReloadAction(GameObjectAnchor gunAnchor)
    {
        _gunAnchor = gunAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _gunAnchor.TryGetComponentOrOwner(stateMachine, out _gunController, this);
    }

    public override void OnStateEnter()
    {
        if (_gunController != null)
        {
            _gunController.Reload();
        }
    }

    public override void OnUpdate() { }
}
