using UnityEngine;

// Just an ID, like CooldownSO: the count lives in the character's CounterController,
// so two enemies using the same CounterSO each count on their own.
[CreateAssetMenu(fileName = "NewCounter",
    menuName = "YUKI Learning State Machine/Counter")]
public class CounterSO : ScriptableObject
{
}
