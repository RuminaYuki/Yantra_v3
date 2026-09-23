using UnityEngine;

[CreateAssetMenu(fileName = "NewAnimFinishTimer_Modifier", 
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Condition/Set Animation Finished Extra Seconds")]
public class SetAnimationFinishedExtraSecondsModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private AnimationFinishedConditionSO _target;
    [SerializeField] private float _extraSeconds;

    public void Apply()
    {
        _target.ExtraSeconds = _extraSeconds;
    }
}
