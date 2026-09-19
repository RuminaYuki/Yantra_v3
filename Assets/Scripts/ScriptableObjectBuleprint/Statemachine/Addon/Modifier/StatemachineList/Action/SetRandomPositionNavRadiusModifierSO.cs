using UnityEngine;

[CreateAssetMenu(fileName = "NewSetRanPosNav_ModifierSO",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/SetRandomPositionNavRadiusModifier")]
public class SetRandomPositionNavRadiusModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private SetRandomPositionNavRadiusActionSO _target;
    [SerializeField] private float _radius;
    [SerializeField] private float _minDistance;
    public void Apply()
    {
        _target.Radius = _radius;
        _target.MinDistance = _minDistance;
    }
}
