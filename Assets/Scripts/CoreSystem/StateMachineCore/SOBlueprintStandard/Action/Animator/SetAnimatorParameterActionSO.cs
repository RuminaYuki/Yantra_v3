using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Yuki.Learning.StateMachine;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Animator))]
[CreateAssetMenu(
    fileName = "SetParametersAnimator_Action", 
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Animator/SetParametersAnimatorAction")]
public class SetAnimatorParameterActionSO : StateActionSO  
{
    public ParameterSetting ParameterSetting;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetParameterAnimatorAction(ParameterSetting);
    }
}

public class SetParameterAnimatorAction : StateAction
{
    private AnchorReference<Animator> _animator;

    private readonly ParameterType _parameterType;
    private readonly string _parameterName;
    private readonly bool _boolValueOnEnter;
    private readonly bool _resetOnExit;

    private bool _previousBoolValue;
    private bool _isApplied;

    public SetParameterAnimatorAction(ParameterSetting parameterSetting)
    {
        _parameterType = parameterSetting.ParameterType;
        _parameterName = parameterSetting.ParameterName;
        _boolValueOnEnter = parameterSetting.BoolValueOnEnter;
        _resetOnExit = parameterSetting.ResetOnExit;
    }
    public override void Awake(StateMachine stateMachine)
    {
        _animator = new AnchorReference<Animator>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        if (!_animator.TryGet(out Animator animator)) return;

        switch (_parameterType)
        {
            case ParameterType.Bool:
                _previousBoolValue = animator.GetBool(_parameterName);
                animator.SetBool(_parameterName, _boolValueOnEnter);
                _isApplied = true;
                break;
            case ParameterType.Trigger:
                animator.SetTrigger(_parameterName);
                break;
        }
    }

    public override void OnStateExit()
    {
        if (_parameterType != ParameterType.Bool || !_isApplied ||
            !_animator.TryGet(out Animator animator))
            return;

        if (_resetOnExit)
            animator.SetBool(_parameterName, _previousBoolValue);

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

