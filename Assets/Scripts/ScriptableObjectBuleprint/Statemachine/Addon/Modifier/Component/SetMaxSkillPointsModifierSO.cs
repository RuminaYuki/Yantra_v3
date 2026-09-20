using UnityEngine;

[CreateAssetMenu(fileName = "NewMaxSkillPoint_Modifier",
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/Component/Set Max Skill Points")]
public class SetMaxSkillPointsModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private GameObjectAnchor _anchor;
    [SerializeField] private float _maxSkillPoints;

    public void Apply()
    {
        _anchor.Value.GetComponent<SkillPoints>().MaxSkillPoints = _maxSkillPoints;
    }
}
