using UnityEngine;

// Just an ID, like CooldownSO: one choice in a RandomPickController pool (an attack, an idle, a line
// to say...). Random Picked (RandomPickedConditionSO) checks whether it was the last pick.
[CreateAssetMenu(fileName = "New_RandomOption",
    menuName = "YUKI Learning State Machine/Random Pick/Random Option")]
public class RandomOptionSO : ScriptableObject
{
}
