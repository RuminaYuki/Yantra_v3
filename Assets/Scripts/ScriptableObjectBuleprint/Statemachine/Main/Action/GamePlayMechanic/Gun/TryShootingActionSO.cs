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
        if (_gunAnchor != null)
        {
            if (_gunAnchor.Value == null)
            {
                Debug.LogWarning(
                    $"{_gunAnchor.name} has not been provided a value yet; TryShootingAction cannot resolve a GunController."
                );
                return;
            }
            _gunController = _gunAnchor.Value.gameObject.GetComponent<GunController>();
            if (_gunController == null)
            {
                Debug.LogWarning(
                    $"GunController component was not found on the GameObject referenced by {_gunAnchor.name}."
                );
            }
            return;
        }
        _gunController = stateMachine.GetComponent<GunController>();

        if (_gunController == null)
            Debug.LogError(
                "TryShootingAction requires GunController.",
                stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        _gunController?.TryShooting();
    }

    public override void OnUpdate() { }
}
