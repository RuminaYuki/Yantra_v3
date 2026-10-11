using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(LineOfSight))]
[CreateAssetMenu(
    fileName = "Detect_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/LineOfSight/Detect")]
public class DetectConditionSO : StateConditionSO
{
    [Header("Notice Settings (overrides values on LineOfSight)")]
    [SerializeField, Min(0f)] private float _detectRange = 10f;
    [SerializeField, Min(0f)] private float _minTimeToNotice = 0.5f;
    [SerializeField, Min(0f)] private float _maxTimeToNotice = 3f;
    [Tooltip("X = normalized distance (0 = closest, 1 = farthest), Y = 0-1 ratio between min-max")]
    [SerializeField] private AnimationCurve _noticeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public float DetectRange
    {
        get => _detectRange;
        set
        {
            if (value < 0f)
            {
                Debug.LogWarning("Detect range cannot be negative. Setting to 0.");
                _detectRange = 0f;
                return;
            }
            _detectRange = value;
        }
    }

    public float MinTimeToNotice
    {
        get => _minTimeToNotice;
        set
        {
            if (value < 0f)
            {
                Debug.LogWarning("Min time to notice cannot be negative. Setting to 0.");
                _minTimeToNotice = 0f;
                return;
            }
            _minTimeToNotice = value;
        }
    }

    public float MaxTimeToNotice
    {
        get => _maxTimeToNotice;
        set
        {
            if (value < 0f)
            {
                Debug.LogWarning("Max time to notice cannot be negative. Setting to 0.");
                _maxTimeToNotice = 0f;
                return;
            }
            _maxTimeToNotice = value;
        }
    }

    public AnimationCurve NoticeCurve
    {
        get => _noticeCurve;
        set
        {
            if (value == null)
            {
                Debug.LogWarning("Notice curve cannot be null. Ignoring.");
                return;
            }
            _noticeCurve = value;
        }
    }

    protected override Condition OnCreateCondition()
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
    private AnchorReference<LineOfSight> _lineOfSight;

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
        _lineOfSight = new AnchorReference<LineOfSight>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        if (!_lineOfSight.TryGet(out LineOfSight lineOfSight)) return;
        lineOfSight.DetectRange = _detectRange;
        lineOfSight.MinTimeToNotice = _minTimeToNotice;
        lineOfSight.MaxTimeToNotice = _maxTimeToNotice;
        lineOfSight.NoticeCurve = _noticeCurve;
        lineOfSight.ResetnoticTimer();
    }
    protected override bool Statement()
    {
        return _lineOfSight.TryGet(out LineOfSight lineOfSight) && lineOfSight.HasNoticed;
    }
}
