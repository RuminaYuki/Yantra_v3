using UnityEngine;
using UnityEngine.InputSystem;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "InputAction_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/Input Action")]
public class InputActionConditionSO : StateConditionSO
{
    [SerializeField] private InputActionReference _inputAction;
    [SerializeField] private InputCheckType _checkType;

    public override Condition CreateCondition()
    {
        return new InputActionCondition(_inputAction,_checkType);
    }
}

public class InputActionCondition : Condition
{
    [SerializeField] private InputActionReference _inputAction;
    [SerializeField] private InputCheckType _checkType;

    public InputActionCondition(InputActionReference inputAction,InputCheckType checkType)
    {
        _inputAction = inputAction;
        _checkType = checkType;
    }

    protected override bool Statement()
    {
        InputAction action = _inputAction?.action;

        if (action == null)
        {
            Debug.LogError("Input Action is null.");
            return false;
        }

        bool result = _checkType switch
        {
            InputCheckType.PerformedThisFrame =>
                action.WasPerformedThisFrame(),

            InputCheckType.Held =>
                action.IsPressed(),

            InputCheckType.ReleasedThisFrame =>
                action.WasReleasedThisFrame(),

            _ => false
        };

        return result;
    }
}
public enum InputCheckType
{
    PerformedThisFrame,
    Held,
    ReleasedThisFrame
}