using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "LostSight_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/LineOfSight/Lost Sight")]
public class LostSightConditionSO : StateConditionSO
{
    [Tooltip("How long the target must be continuously out of raw sight (LineOfSight.CanSeeTarget) before this is true.")]
    [SerializeField, Min(0f)] private float _loseSightDuration = 3f;

    public float LoseSightDuration
    {
        get => _loseSightDuration;
        set
        {
            if (value < 0f)
            {
                Debug.LogWarning("Lose sight duration cannot be negative. Setting to 0.");
                _loseSightDuration = 0f;
                return;
            }
            _loseSightDuration = value;
        }
    }

    public override Condition CreateCondition()
    {
        return new LostSightCondition(_loseSightDuration);
    }
}

public class LostSightCondition : Condition
{
    private readonly float _loseSightDuration;
    private LineOfSight _lineOfSight;

    public LostSightCondition(float loseSightDuration)
    {
        _loseSightDuration = loseSightDuration;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _lineOfSight = stateMachine.GetComponent<LineOfSight>();

        if (_lineOfSight == null)
        {
            Debug.LogError("LostSightCondition requires a LineOfSight component on the same GameObject.");
        }
    }

    protected override bool Statement()
    {
        return _lineOfSight != null && _lineOfSight.TimeSinceLastSeen >= _loseSightDuration;
    }
}
