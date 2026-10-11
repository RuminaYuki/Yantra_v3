using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(LineOfSight))]
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

    protected override Condition OnCreateCondition()
    {
        return new LostSightCondition(_loseSightDuration);
    }
}

public class LostSightCondition : Condition
{
    private readonly float _loseSightDuration;
    private AnchorReference<LineOfSight> _lineOfSight;

    public LostSightCondition(float loseSightDuration)
    {
        _loseSightDuration = loseSightDuration;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _lineOfSight = new AnchorReference<LineOfSight>(stateMachine, this);
    }

    protected override bool Statement()
    {
        return _lineOfSight.TryGet(out LineOfSight lineOfSight) &&
               lineOfSight.TimeSinceLastSeen >= _loseSightDuration;
    }
}
