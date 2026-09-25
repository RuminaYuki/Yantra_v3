using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "EnableCharacterController_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/CharacterController/Enable Character Controller")]
public class EnableCharacterControllerActionSO : StateActionSO
{
    [SerializeField] private bool value = false;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new EnableCharacterControllerAction(value, resetValueOnExit);
    }
}

public class EnableCharacterControllerAction : StateAction
{
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private CharacterController characterController;

    public EnableCharacterControllerAction(bool value, bool resetValueOnExit)
    {
        this.value = value;
        this.resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        characterController = stateMachine.GetComponent<CharacterController>();

        if (characterController == null)
            Debug.LogError("EnableCharacterControllerAction cannot find CharacterController.");
    }

    public override void OnStateEnter()
    {
        if (characterController == null) return;
        characterController.enabled = value;
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (characterController == null || !resetValueOnExit) return;
        characterController.enabled = !value;
    }
}
