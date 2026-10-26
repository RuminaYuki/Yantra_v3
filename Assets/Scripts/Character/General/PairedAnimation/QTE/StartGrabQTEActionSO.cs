using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// ใส่ใน State จับของผี — เข้า State แล้วสั่ง GrabQTEInteractAddition (อยู่ตัวลูกของผี) ให้เริ่ม QTE กับผู้เล่น
[CreateAssetMenu(
    fileName = "StartGrabQTE_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Paired Animation/Start Grab QTE")]
public class StartGrabQTEActionSO : StateActionSO
{
    [SerializeField] private string _playerTag = "Player";

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new StartGrabQTEAction(_playerTag);
    }
}

public class StartGrabQTEAction : StateAction
{
    private readonly string _playerTag;
    private GrabQTEInteractAddition _qte;

    public StartGrabQTEAction(string playerTag)
    {
        _playerTag = playerTag;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _qte = stateMachine.Owner.GetComponentInChildren<GrabQTEInteractAddition>(true);
        if (_qte == null)
            _qte = stateMachine.Owner.GetComponentInParent<GrabQTEInteractAddition>();

        if (_qte == null)
            Debug.LogError($"[StartGrabQTEAction] หา GrabQTEInteractAddition ของ {stateMachine.Owner.name} ไม่เจอ", stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        if (_qte == null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
        if (player == null)
            return;

        // ระบบ Interact ใช้ตัวบนสุดของผู้เล่น (Rin(Player)) เป็น rootPlayer
        _qte.BeginQTE(player.transform.root.gameObject);
    }

    public override void OnUpdate()
    {
    }
}
