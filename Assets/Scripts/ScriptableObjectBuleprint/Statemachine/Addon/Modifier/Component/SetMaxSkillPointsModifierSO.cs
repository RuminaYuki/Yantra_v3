using UnityEngine;
using Yuki.Learning.StateMachine;

[CreateAssetMenu(fileName = "NewMaxSkillPoint_Modifier",
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/Component/Set Max Skill Points")]
public class SetMaxSkillPointsModifierSO : ScriptableObject, IOwnerStatModifier
{
    [Tooltip("Leave empty to use the character itself")]
    [SerializeField] private AnchorIDSO _anchor;
    [SerializeField] private float _maxSkillPoints;

    public void Apply(GameObject owner)
    {
        if (ComponentModifierTarget.TryGet(owner, _anchor, this, out SkillPoints skillPoints))
            skillPoints.MaxSkillPoints = _maxSkillPoints;
    }
}
