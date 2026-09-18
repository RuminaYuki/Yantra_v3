using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "Detect_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/LineOfSight/Detect")]
public class DetectConditionSO : StateConditionSO
{
    [Header("Notice Settings (overrides values on LineOfSight)")]
    [SerializeField, Min(0f)] private float _detectRange = 10f;
    [SerializeField, Min(0f)] private float _minTimeToNotice = 0.5f;
    [SerializeField, Min(0f)] private float _maxTimeToNotice = 3f;
    [Tooltip("X = normalized distance (0 = closest, 1 = farthest), Y = 0-1 ratio between min-max")]
    [SerializeField] private AnimationCurve _noticeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public override Condition CreateCondition()
    {
        return new DetectCondition(_detectRange, _minTimeToNotice, _maxTimeToNotice, _noticeCurve);
    }
}

public class DetectCondition : Condition
{
    private readonly float _detectRange;
    private readonly float _minTimeToNotice;
    private readonly float _maxTimeToNotice;
    private readonly AnimationCurve _noticeCurve;
    private LineOfSight _lineOfSight;

    public DetectCondition(
        float detectRange,
        float minTimeToNotice,
        float maxTimeToNotice,
        AnimationCurve noticeCurve)
    {
        _detectRange = detectRange;
        _minTimeToNotice = minTimeToNotice;
        _maxTimeToNotice = maxTimeToNotice;
        _noticeCurve = noticeCurve;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _lineOfSight = stateMachine.GetComponent<LineOfSight>();

        if (_lineOfSight == null)
        {
            Debug.LogError("DetectCondition requires a LineOfSight component on the same GameObject.");
        }
    }
    public override void OnStateEnter()
    {
        if (_lineOfSight == null)
        {
            return;
        }

        _lineOfSight.DetectRange = _detectRange;
        _lineOfSight.MinTimeToNotice = _minTimeToNotice;
        _lineOfSight.MaxTimeToNotice = _maxTimeToNotice;
        _lineOfSight.NoticeCurve = _noticeCurve;
        _lineOfSight.ResetnoticTimer();
    }
    protected override bool Statement()
    {
        return _lineOfSight != null && _lineOfSight.HasNoticed;
    }
}
