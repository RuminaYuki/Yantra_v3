using UnityEngine;

[CreateAssetMenu(fileName = "NewDetectSettings_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Condition/Set Detect Settings")]
public class SetDetectSettingsModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private DetectConditionSO _target;
    [SerializeField] private float _detectRange;
    [SerializeField] private float _minTimeToNotice;
    [SerializeField] private float _maxTimeToNotice;
    [SerializeField] private AnimationCurve _noticeCurve;

    public void Apply()
    {
        _target.DetectRange = _detectRange;
        _target.MinTimeToNotice = _minTimeToNotice;
        _target.MaxTimeToNotice = _maxTimeToNotice;
        _target.NoticeCurve = _noticeCurve;
    }
}
