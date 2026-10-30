using UnityEngine;
using UnityEngine.Splines;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Self Anchor ว่าง = ใช้ SplineInstantiate บน Owner / ใส่ Anchor = ใช้ตัวที่ Anchor ชี้ (เช่น อยู่ตัวลูก)
// ตั้ง parameter ให้ Animator ของทุก instance ที่ SplineInstantiate สร้างไว้ (เช่น หนามทุกอัน)
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(SplineInstantiate))]
[CreateAssetMenu(
    fileName = "SetSplineItemsAnimatorParameter_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Spline/Set Spline Items Animator Parameter")]
public class SetSplineItemsAnimatorParameterActionSO : StateActionSO
{
    public ParameterSetting ParameterSetting;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetSplineItemsAnimatorParameterAction(ParameterSetting);
    }
}

public class SetSplineItemsAnimatorParameterAction : StateAction
{
    private AnchorReference<SplineInstantiate> _splineInstantiate;

    private readonly ParameterType _parameterType;
    private readonly int _parameterHash;
    private readonly bool _boolValueOnEnter;
    private readonly bool _resetOnExit;

    // Instances are found on enter (they may have been regenerated since last time),
    // and kept so OnStateExit restores the same ones.
    private Animator[] _animators;
    private bool[] _previousBoolValues;

    public SetSplineItemsAnimatorParameterAction(ParameterSetting parameterSetting)
    {
        _parameterType = parameterSetting.ParameterType;
        _parameterHash = Animator.StringToHash(parameterSetting.ParameterName);
        _boolValueOnEnter = parameterSetting.BoolValueOnEnter;
        _resetOnExit = parameterSetting.ResetOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _splineInstantiate = new AnchorReference<SplineInstantiate>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_splineInstantiate.TryGet(out SplineInstantiate spline)) return;

        // Instances live under a hidden root child of SplineInstantiate - still found by GetComponentsInChildren.
        _animators = spline.GetComponentsInChildren<Animator>(true);
        _previousBoolValues = new bool[_animators.Length];

        for (int i = 0; i < _animators.Length; i++)
        {
            Animator animator = _animators[i];
            switch (_parameterType)
            {
                case ParameterType.Bool:
                    _previousBoolValues[i] = animator.GetBool(_parameterHash);
                    animator.SetBool(_parameterHash, _boolValueOnEnter);
                    break;
                case ParameterType.Trigger:
                    animator.SetTrigger(_parameterHash);
                    break;
            }
        }
    }

    public override void OnStateExit()
    {
        if (_parameterType != ParameterType.Bool || !_resetOnExit || _animators == null)
            return;

        for (int i = 0; i < _animators.Length; i++)
        {
            // Skip instances destroyed by a regenerate while in this state.
            if (_animators[i] != null)
                _animators[i].SetBool(_parameterHash, _previousBoolValues[i]);
        }

        _animators = null;
    }

    public override void OnUpdate() { }
}
