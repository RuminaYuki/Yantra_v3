using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Yuki.Learning.StateMachine;

[CreateAssetMenu(
    fileName = "KeyDown_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/InputSystem/Key Down")]
public class KeyDownConditionSO : StateConditionSO
{
    [SerializeField] private KeyCode _keyCode = KeyCode.Alpha1;

    public override Condition CreateCondition()
    {
        return new KeyDownCondition(_keyCode);
    }
}
public class KeyDownCondition : Condition
{
    private readonly KeyCode _keyCode;

    public KeyDownCondition(KeyCode keyCode)
    {
        _keyCode = keyCode;
    }

    protected override bool Statement()
    {
        return Input.GetKeyDown(_keyCode);
    }
}

