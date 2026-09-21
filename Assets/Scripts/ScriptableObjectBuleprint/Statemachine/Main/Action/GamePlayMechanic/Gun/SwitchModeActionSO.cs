using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SwitchMode_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Gun/Switch Mode")]
public class SwitchModeActionSO : StateActionSO
{
    [SerializeField] private GameObjectAnchor _gunAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SwitchModeAction(_gunAnchor);
    }
}

public class SwitchModeAction : StateAction
{
    private readonly GameObjectAnchor _gunAnchor;
    private GunController _gunController;
    private SkillPoints _skillPoints;

    public SwitchModeAction(GameObjectAnchor gunAnchor)
    {
        _gunAnchor = gunAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _skillPoints = stateMachine.GetComponent<SkillPoints>();

        if (_gunAnchor != null)
        {
            if (_gunAnchor.Value == null)
            {
                Debug.LogWarning(
                    $"{_gunAnchor.name} has not been provided a value yet; SwitchModeAction cannot resolve a GunController."
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
                "SwitchModeAction requires GunController.",
                stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        if (_gunController == null) return;

        GunController.GunMode desiredMode = GunController.GunMode.Normal;
        if (_skillPoints != null && _skillPoints.CurrentSkillPoints >= _skillPoints.MaxSkillPoints)
        {
            desiredMode = GunController.GunMode.Special;
        }

        if (_gunController.GetGunMode() != desiredMode)
        {
            _gunController.SwitchMode();
        }
    }

    public override void OnUpdate() { }
}
