using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// ใส่ใน State ท่าตีของผี — เข้า State แล้วสั่ง BlockQTE (วางไว้ใต้ตัวผี หรือข้างๆ ตัวที่มี State Machine) ให้เริ่ม: เวลาช้าลง + ป้าย "กด F ค้างไว้"
// แบบเดียวกับ Start Grab QTE
// ผีที่ไม่มี BlockQTE ใต้ตัว = Action นี้ไม่ทำอะไรเลย → ใส่ใน State ท่าตีที่ผีทุกตัวใช้ร่วมกันได้ ไม่ต้องแยกไฟล์
[CreateAssetMenu(
    fileName = "StartBlockQTE_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/BlockSystem/Start Block QTE")]
public class StartBlockQTEActionSO : StateActionSO
{
    [SerializeField] private string _playerTag = "Player";

    [Tooltip("ออกจาก State นี้ก่อนผีตีโดน = เลิก QTE คืนเวลาปกติ\n" +
             "ถ้าท่าง้างกับท่าตีโดนเป็นคนละ State (ใส่ Action นี้ไว้ที่ State ง้าง) ให้เอาติ๊กออก")]
    [SerializeField] private bool _cancelOnExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new StartBlockQTEAction(_playerTag, _cancelOnExit);
    }
}

public class StartBlockQTEAction : StateAction
{
    private readonly string _playerTag;
    private readonly bool _cancelOnExit;
    private BlockQTE _qte;

    public StartBlockQTEAction(string playerTag, bool cancelOnExit)
    {
        _playerTag = playerTag;
        _cancelOnExit = cancelOnExit;
    }

    private Transform _ghost;

    public override void Awake(StateMachine stateMachine)
    {
        _ghost = stateMachine.Owner.transform;
        _qte = BlockQTE.FindFor(_ghost);
        // ไม่เจอ = ผีตัวปกติ ไม่ต้องเตือน
        if (_qte != null)
            Debug.Log($"[BlockQTE] ผูก {_qte.name} กับผี {_ghost.name} แล้ว", _qte);
    }

    public override void OnStateEnter()
    {
        if (_qte == null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
        if (player == null)
            return;

        // ระบบผู้เล่นใช้ตัวบนสุด (Rin(Player)) เป็น root
        // ส่งตัวผีที่ตีจริงไปด้วย → กล้องหันไปหาผีถูกตัว (ตัวบนสุดของผีไม่ขยับตาม)
        _qte.Begin(player.transform.root.gameObject, _ghost);
    }

    public override void OnStateExit()
    {
        if (_cancelOnExit && _qte != null)
            _qte.Cancel();
    }

    public override void OnUpdate()
    {
    }
}