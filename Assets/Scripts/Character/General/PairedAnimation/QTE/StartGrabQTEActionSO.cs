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

    [Tooltip("ออกจาก State นี้ก่อนผู้เล่นดิ้นหลุด / แพ้ = ปิด QTE ทิ้ง\n" +
             "ถ้าจับใน State นี้แล้วไปลากต่อใน State อื่น (QTE ต้องเล่นต่อ) ให้เอาติ๊กออก")]
    [SerializeField] private bool _stopOnExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new StartGrabQTEAction(_playerTag, _stopOnExit);
    }
}

public class StartGrabQTEAction : StateAction
{
    private readonly string _playerTag;
    private readonly bool _stopOnExit;
    private GrabQTEInteractAddition _qte;

    public StartGrabQTEAction(string playerTag, bool stopOnExit)
    {
        _playerTag = playerTag;
        _stopOnExit = stopOnExit;
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

    // ออกจาก State ก่อนผู้เล่นดิ้นหลุด / แพ้ (เช่น ลากถึงเปรตแล้ว) — ปิด QTE ทิ้ง ไม่ให้ค้างบนจอ
    public override void OnStateExit()
    {
        if (_stopOnExit && _qte != null)
            _qte.StopQTE();
    }

    public override void OnUpdate()
    {
    }
}