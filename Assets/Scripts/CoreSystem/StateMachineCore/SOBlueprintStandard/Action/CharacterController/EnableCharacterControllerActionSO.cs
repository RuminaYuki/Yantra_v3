using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(CharacterController))]
[CreateAssetMenu(
    fileName = "EnableCharacterController_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/CharacterController/Enable Character Controller")]
public class EnableCharacterControllerActionSO : StateActionSO
{
    [SerializeField] private bool value = false;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new EnableCharacterControllerAction(value, resetValueOnExit);
    }
}

public class EnableCharacterControllerAction : StateAction
{
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private AnchorReference<CharacterController> characterController;

    public EnableCharacterControllerAction(bool value, bool resetValueOnExit)
    {
        this.value = value;
        this.resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        characterController = new AnchorReference<CharacterController>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!characterController.TryGet(out CharacterController controller)) return;
        controller.enabled = value;
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (!resetValueOnExit || !characterController.TryGet(out CharacterController controller)) return;
        controller.enabled = !value;
    }
}
