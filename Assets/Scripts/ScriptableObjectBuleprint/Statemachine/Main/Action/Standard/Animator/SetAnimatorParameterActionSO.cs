using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Yuki.Learning.StateMachine;

[CreateAssetMenu(
    fileName = "SetParametersAnimator_Action", 
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Standard/Animator/SetParametersAnimatorAction")]
public class SetAnimatorParameterActionSO : StateActionSO  
{
    public ParameterSetting ParameterSetting;

    [Header("If the TargetAnchor is set, Check target flag instead")]
    [SerializeField] private AnimatorAnchor targetAnchor;
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetParameterAnimatorAction(ParameterSetting, targetAnchor);
    }
}

public class SetParameterAnimatorAction : StateAction
{
    private Animator _animator;

    private readonly ParameterType _parameterType;
    private readonly string _parameterName;
    private readonly bool _boolValueOnEnter;
    private readonly bool _resetOnExit;
    private readonly AnimatorAnchor _targetAnchor;

    private bool _previousBoolValue;
    private bool _isApplied;

    public SetParameterAnimatorAction(ParameterSetting parameterSetting, AnimatorAnchor targetAnchor = null)
    {
        _parameterType = parameterSetting.ParameterType;
        _parameterName = parameterSetting.ParameterName;
        _boolValueOnEnter = parameterSetting.BoolValueOnEnter;
        _resetOnExit = parameterSetting.ResetOnExit;

        this._targetAnchor = targetAnchor;
    }
    public override void Awake(StateMachine stateMachine)
    {
        if(_targetAnchor != null)
        {
            _animator = _targetAnchor.Value;
            return;
        }
        _animator = stateMachine.GetComponent<Animator>();
    }
    public override void OnStateEnter()
    {
        switch (_parameterType)
        {
            case ParameterType.Bool:
                _previousBoolValue = _animator.GetBool(_parameterName);
                _animator.SetBool(_parameterName, _boolValueOnEnter);
                _isApplied = true;
                break;
            case ParameterType.Trigger:
                _animator.SetTrigger(_parameterName);
                break;
        }
    }

    public override void OnStateExit()
    {
        if (_animator == null || _parameterType != ParameterType.Bool || !_isApplied)
            return;

        if (_resetOnExit)
            _animator.SetBool(_parameterName, _previousBoolValue);

        _isApplied = false;
    }

    public override void OnUpdate(){}
}

[System.Serializable]
public struct ParameterSetting
{
    public ParameterType ParameterType;
    public string ParameterName;

    [Header("Bool Settings")]
    [Tooltip("Value to set when entering the state.")]
    public bool BoolValueOnEnter;
    [Tooltip("If enabled, restores the bool to whatever value it had before entering this state.")]
    public bool ResetOnExit;
}

public enum ParameterType
{
    Bool, Trigger
}

