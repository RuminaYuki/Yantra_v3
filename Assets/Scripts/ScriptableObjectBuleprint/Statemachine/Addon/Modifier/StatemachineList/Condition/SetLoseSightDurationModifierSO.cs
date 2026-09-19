using UnityEngine;

[CreateAssetMenu(fileName = "NewLoseSightDuration_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Condition/Set Lose Sight Duration")]
public class SetLoseSightDurationModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private LostSightConditionSO _target;
    [SerializeField] private float _loseSightDuration;

    public void Apply()
    {
        _target.LoseSightDuration = _loseSightDuration;
    }
}
