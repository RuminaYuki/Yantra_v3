using UnityEngine;
using UnityEngine.InputSystem;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "InputAction_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/InputSystem/Input Action")]
public class InputActionConditionSO : StateConditionSO
{
    [SerializeField] private InputActionReference _inputAction;
    [SerializeField] private InputCheckType _checkType;

    [Tooltip("Buffer window (seconds) for PerformedThisFrame/ReleasedThisFrame so the press isn't lost while waiting on other AND-conditions. 0 = old single-frame behavior.")]
    [SerializeField, Min(0f)] private float _bufferTime = 0.15f;
    [SerializeField] private bool _jumpBuffering = false;

    public override Condition CreateCondition()
    {
        return new InputActionCondition(_inputAction, _checkType, _bufferTime,_jumpBuffering);
    }
}

public class InputActionCondition : Condition
{
    private readonly InputActionReference _inputAction;
    private readonly InputCheckType _checkType;
    private readonly float _bufferTime;
    private readonly bool _jumpBuffering;

    private float _performedBufferedUntil = -1f;
    private float _releasedBufferedUntil = -1f;

    public InputActionCondition(InputActionReference inputAction, InputCheckType checkType, float bufferTime, bool jumpBuffering)
    {
        _inputAction = inputAction;
        _checkType = checkType;
        _bufferTime = bufferTime;
        _jumpBuffering = jumpBuffering;
    }

    public override void Awake(StateMachine stateMachine)
    {
        InputAction action = _inputAction?.action;
        if (action == null)
            return;

        action.performed += OnPerformed;
        action.canceled += OnCanceled;
    }

    public override void Dispose()
    {
        InputAction action = _inputAction?.action;
        if (action == null)
            return;

        action.performed -= OnPerformed;
        action.canceled -= OnCanceled;
    }
    public override void OnStateEnter()
    {
        if(_jumpBuffering) return;
        _performedBufferedUntil = -1f;
        _releasedBufferedUntil = -1f;
    }

    private void OnPerformed(InputAction.CallbackContext ctx) =>
        _performedBufferedUntil = Time.time + _bufferTime;

    private void OnCanceled(InputAction.CallbackContext ctx) =>
        _releasedBufferedUntil = Time.time + _bufferTime;

    protected override bool Statement()
    {
        InputAction action = _inputAction?.action;

        if (action == null)
        {
            Debug.LogError("Input Action is null.");
            return false;
        }

        return _checkType switch
        {
            InputCheckType.PerformedThisFrame => Time.time <= _performedBufferedUntil,
            InputCheckType.Held => action.IsPressed(),
            InputCheckType.ReleasedThisFrame => Time.time <= _releasedBufferedUntil,
            _ => false
        };
    }

    // Optional: call once the transition that consumed this buffered input actually fires,
    // so the same press can't satisfy the condition twice across separate transitions.
    public void ConsumePerformed() => _performedBufferedUntil = -1f;
    public void ConsumeReleased() => _releasedBufferedUntil = -1f;
}

public enum InputCheckType
{
    PerformedThisFrame,
    Held,
    ReleasedThisFrame
}
