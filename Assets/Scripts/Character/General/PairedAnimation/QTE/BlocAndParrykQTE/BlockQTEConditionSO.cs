using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// ให้ State Machine ของผีอ่านผล QTE กันผี
// เช่น Stance → Pursue เมื่อ Blocked / Failed = ไม่ต้องยืนรอคูลดาวน์ เดินเข้าหาผู้เล่นเลย
// ผลค้างอยู่จนผีเริ่มท่าตีครั้งถัดไป (ตอนนั้นล้างเป็นไม่มีผล)
[CreateAssetMenu(
    fileName = "BlockQTE_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/GamePlayMechanic/Block QTE")]
public class BlockQTEConditionSO : StateConditionSO
{
    public enum Check
    {
        IsRunning,       // กำลังรอผู้เล่นกด (เวลาช้าอยู่)
        Blocked,         // ผลล่าสุด: กันได้
        Failed,          // ผลล่าสุด: พลาด โดนตี
        HasEverBlocked   // เคยกันได้แล้ว (จบ Tutorial นี้แล้ว)
    }

    [SerializeField] private Check _check = Check.Blocked;

    protected override Condition OnCreateCondition() => new BlockQTECondition(_check);
}

public class BlockQTECondition : Condition
{
    private readonly BlockQTEConditionSO.Check _check;
    private BlockQTE _qte;

    public BlockQTECondition(BlockQTEConditionSO.Check check)
    {
        _check = check;
    }

    public override void Awake(StateMachine stateMachine)
    {
        // ใต้ตัว / ตัวแม่ / วางข้างๆ ตัวที่มี State Machine ก็เจอ — ผีที่ไม่มี BlockQTE = เป็นเท็จตลอด
        _qte = BlockQTE.FindFor(stateMachine.Owner.transform);
    }

    protected override bool Statement()
    {
        if (_qte == null)
            return false;

        switch (_check)
        {
            case BlockQTEConditionSO.Check.IsRunning: return _qte.IsRunning;
            case BlockQTEConditionSO.Check.Blocked: return _qte.LastResult == BlockQTE.Result.Blocked;
            case BlockQTEConditionSO.Check.Failed: return _qte.LastResult == BlockQTE.Result.Failed;
            default: return _qte.HasEverBlocked;
        }
    }
}