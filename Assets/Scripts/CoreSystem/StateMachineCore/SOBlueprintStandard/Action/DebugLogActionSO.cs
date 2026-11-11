using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Writes a message to the Console when the state enters / exits (or every frame), for checking flow.
// Clicking the log in the Console pings the owner GameObject.
[MenuGroup("Standard")]
[CreateAssetMenu(
    fileName = "NewDebugLog_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Debug Log")]
public class DebugLogActionSO : StateActionSO
{
    public enum LogType { Log, Warning, Error }

    [SerializeField, TextArea] private string _message = "Hello";
    [SerializeField] private LogType _logType = LogType.Log;

    [Header("When")]
    [SerializeField] private bool _onEnter = true;
    [SerializeField] private bool _onUpdate = false;
    [SerializeField] private bool _onExit = false;

    [Tooltip("Adds [Owner name] at the front of the message.")]
    [SerializeField] private bool _prefixOwnerName = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new DebugLogAction(_message, _logType, _onEnter, _onUpdate, _onExit, _prefixOwnerName);
    }
}

public class DebugLogAction : StateAction
{
    private readonly string _message;
    private readonly DebugLogActionSO.LogType _logType;
    private readonly bool _onEnter;
    private readonly bool _onUpdate;
    private readonly bool _onExit;
    private readonly bool _prefixOwnerName;
    private GameObject _owner;

    public DebugLogAction(
        string message,
        DebugLogActionSO.LogType logType,
        bool onEnter,
        bool onUpdate,
        bool onExit,
        bool prefixOwnerName)
    {
        _message = message;
        _logType = logType;
        _onEnter = onEnter;
        _onUpdate = onUpdate;
        _onExit = onExit;
        _prefixOwnerName = prefixOwnerName;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner;
    }

    public override void OnStateEnter()
    {
        if (_onEnter) Write("Enter");
    }

    public override void OnUpdate()
    {
        if (_onUpdate) Write("Update");
    }

    public override void OnStateExit()
    {
        if (_onExit) Write("Exit");
    }

    private void Write(string when)
    {
        string text = _prefixOwnerName && _owner != null
            ? $"[{_owner.name}] ({when}) {_message}"
            : $"({when}) {_message}";

        switch (_logType)
        {
            case DebugLogActionSO.LogType.Warning:
                Debug.LogWarning(text, _owner);
                break;
            case DebugLogActionSO.LogType.Error:
                Debug.LogError(text, _owner);
                break;
            default:
                Debug.Log(text, _owner);
                break;
        }
    }
}
