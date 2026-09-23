using UnityEngine;

// Purely visual — drop one of these into a CharacterStatsSO's Modifiers list to break the
// Inspector up into labeled sections (e.g. "Attributes", "State", "Attack"), the same way
// the old PlayerStatsSO used [Header("...")]. Apply() does nothing.
[CreateAssetMenu(fileName = "NewHeader_Modifier",
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/Header")]
public class HeaderModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private string _label = "Header";
    public string Label => _label;

    public void Apply() { }
}
