using UnityEngine;

// Just an ID for one set of options in a RandomPickController, so a character can roll its attacks
// and its idles separately. The options and weights live in the controller, so two characters
// using the same RandomPoolSO each have their own.
[CreateAssetMenu(fileName = "NewRandomPool",
    menuName = "YUKI Learning State Machine/Random Pick/Random Pool")]
public class RandomPoolSO : ScriptableObject
{
}
