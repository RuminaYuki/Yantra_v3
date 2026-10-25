using UnityEngine;

// Just an ID, like FlagSO: the time left lives in the character's CooldownController,
// so two enemies using the same CooldownSO each have their own.
[CreateAssetMenu(fileName = "NewCooldown",
    menuName = "YUKI Learning State Machine/Cooldown")]
public class CooldownSO : ScriptableObject
{
}
