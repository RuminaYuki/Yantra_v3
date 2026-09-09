using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Yuki.Learning.StateMachine;

[CreateAssetMenu(
    fileName = "SetParametersAnimatorActionSO", 
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Animator/SetParametersAnimatorAction")]
public class SetAnimatorParameterActionSO : StateActionSO  
{
    public ParameterSetting ParameterSetting;
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetParameterAnimatorAction(ParameterSetting);
    }
}

public class SetParameterAnimatorAction : StateAction
{
    private Animator _animator;

    private readonly ParameterType _parameterType;
    private readonly string _parameterName;

    public SetParameterAnimatorAction(ParameterSetting parameterSetting)
    {
        _parameterType = parameterSetting.ParameterType;
        _parameterName = parameterSetting.ParameterName;
    }
    public override void Awake(StateMachine stateMachine)
    {
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

