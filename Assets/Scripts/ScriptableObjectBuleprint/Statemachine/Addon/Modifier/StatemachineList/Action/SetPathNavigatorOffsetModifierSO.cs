using UnityEngine;

[CreateAssetMenu(fileName = "NewPathNavigatorOffset_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/Set Path Navigator Offset")]
public class SetPathNavigatorOffsetModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private SetPathNavigatorTargetActionSO _target;
    [SerializeField] private Vector3 _offset;

    public void Apply()
    {
        _target.Offset = _offset;
    }
}
