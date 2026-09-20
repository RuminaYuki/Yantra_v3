using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Character_Stats", menuName = "YUKI Learning State Machine/StatsParameter/CharacterStatsSO")]
public class CharacterStatsSO : ScriptableObject
{
    [SerializeField] private List<ScriptableObject> _modifiers;

    public void ApplyStats()
    {
        foreach (var modifier in _modifiers)
            (modifier as IStatModifier)?.Apply();
    }
}
