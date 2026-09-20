using UnityEngine;

[CreateAssetMenu(fileName = "NewGainSP_Modifier",
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/Set Gain Skill Point")]
public class SetGainSkillPointModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private GianSkillOnHitPointActionSO _target;
    [SerializeField] private float _amount;

    public void Apply()
    {
        _target.GianSkillPointAmount = _amount;
    }
}
