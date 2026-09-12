using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "AnyKey_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/InputSystem/Any Key")]
public class AnyKeyConditionSO : StateConditionSO
{
    [SerializeField] private InputCheckType _checkType;
    [SerializeField] private InputActionReference[] _excludedActions;

    public override Condition CreateCondition()
    {
        return new AnyKeyCondition(_checkType, _excludedActions);
    }
}

public class AnyKeyCondition : Condition
{
    private readonly InputCheckType _checkType;
    private readonly InputActionReference[] _excludedActions;

    public AnyKeyCondition(InputCheckType checkType, InputActionReference[] excludedActions)
    {
        _checkType = checkType;
        _excludedActions = excludedActions ?? Array.Empty<InputActionReference>();
    }

    private bool IsExcluded(ButtonControl button)
    {
        foreach (InputActionReference actionRef in _excludedActions)
        {
            if (actionRef == null)
            {
                continue;
            }

            InputAction action = actionRef.action;

            if (action == null)
            {
                continue;
            }

            foreach (InputControl control in action.controls)
            {
                if (control == button)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CheckButton(ButtonControl button)
    {
        if (IsExcluded(button))
        {
            return false;
        }

        return _checkType switch
        {
            InputCheckType.PerformedThisFrame => button.wasPressedThisFrame,
            InputCheckType.Held => button.isPressed,
            InputCheckType.ReleasedThisFrame => button.wasReleasedThisFrame,
            _ => false
        };
    }

    protected override bool Statement()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            foreach (KeyControl key in keyboard.allKeys)
            {
                if (CheckButton(key))
                {
                    return true;
                }
            }
        }

        Mouse mouse = Mouse.current;

        if (mouse != null)
        {
            if (CheckButton(mouse.leftButton) ||
                CheckButton(mouse.rightButton) ||
                CheckButton(mouse.middleButton) ||
                CheckButton(mouse.forwardButton) ||
                CheckButton(mouse.backButton))
            {
                return true;
            }
        }

        return false;
    }
}
