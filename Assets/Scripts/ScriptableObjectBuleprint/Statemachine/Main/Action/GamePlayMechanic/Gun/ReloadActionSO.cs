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
        if (_gunAnchor != null)
        {
            if (_gunAnchor.Value == null)
            {
                Debug.LogWarning(
                    $"{_gunAnchor.name} has not been provided a value yet; ReloadAction cannot resolve a GunController."
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
                "ReloadAction requires GunController.",
                stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        _gunController?.Reload();
    }

    public override void OnUpdate() { }
}
