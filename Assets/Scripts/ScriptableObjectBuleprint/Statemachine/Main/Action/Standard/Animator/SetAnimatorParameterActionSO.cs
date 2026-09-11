using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Yuki.Learning.StateMachine;

[CreateAssetMenu(
    fileName = "SetParametersAnimator_ActionSO", 
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
    private readonly AnimatorAnchor _targetAnchor;

    public SetParameterAnimatorAction(ParameterSetting parameterSetting, AnimatorAnchor targetAnchor = null)
    {
        _parameterType = parameterSetting.ParameterType;
        _parameterName = parameterSetting.ParameterName;

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
                _animator.SetBool(_parameterName, true);
                break;
            case ParameterType.Trigger:
                _animator.SetTrigger(_parameterName);
                break;
        }
    }

    public override void OnStateExit()
    {
        if (_animator == null || _parameterType != ParameterType.Bool)
            return;

        _animator.SetBool(_parameterName, false);
    }

    public override void OnUpdate(){}
}

[System.Serializable]
public struct ParameterSetting
{
    public ParameterType ParameterType;
    public string ParameterName;
}

public enum ParameterType
{
    Bool, Trigger
}

