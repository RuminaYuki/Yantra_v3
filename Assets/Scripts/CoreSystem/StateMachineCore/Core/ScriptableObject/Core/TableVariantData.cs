using System;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// สิ่งที่ Variant table จดไว้ว่า "ต่างจาก Base ตรงไหน" (เก็บใน TransitionTableSO)
// ไม่ได้จดอะไร = ใช้ของ Base ตามเดิม

// Variant: เปลี่ยนค่าใน field ของ action / condition เฉพาะ table นี้ (asset ตัวจริงไม่ถูกแก้)
[Serializable]
public class FieldOverride : OverrideValue
{
    [SerializeField] private StateElementSO _target;     // เช่น Move_Action
    [SerializeField] private string _fieldPath;          // เช่น "_speed"

    public StateElementSO Target => _target;
    public string FieldPath => _fieldPath;
}

// Variant: ลบ / เพิ่ม action ของ state หนึ่ง เฉพาะ table นี้ (state ของ Base ไม่ถูกแก้)
//   Remove: ใน State ไม่ทำ Action
//   Add:    ใน State ทำ Action เพิ่ม (ต่อท้าย ถ้าไม่ได้จัดลำดับไว้ใน ActionOrder)
[Serializable]
public class ActionChange
{
    // ตัวเลขกำหนดเอง: Unity เซฟเป็นตัวเลข ถ้าลบหรือแทรกชนิดใหม่ ของเก่าจะไม่เปลี่ยนความหมาย
    public enum ChangeKind
    {
        Remove = 1,
        Add = 2
    }

    [SerializeField] private StateSO _state;
    [SerializeField] private ChangeKind _kind;
    [SerializeField] private StateActionSO _action;

    public StateSO State => _state;
    public ChangeKind Kind => _kind;
    public StateActionSO Action => _action;
}

// Variant: ลำดับ action ของ state หนึ่ง เฉพาะ table นี้
// action ที่ไม่อยู่ใน Actions (เช่น Base เพิ่มทีหลัง) ไปต่อท้ายตามลำดับเดิม
[Serializable]
public class ActionOrder
{
    [SerializeField] private StateSO _state;
    [SerializeField] private StateActionSO[] _actions = Array.Empty<StateActionSO>();

    public StateSO State => _state;
    public StateActionSO[] Actions => _actions;
}

// Variant: ใช้เงื่อนไขชุดนี้แทนเงื่อนไขของ transition จาก Base (From / To เหมือนเดิม)
// อ้าง transition ด้วย Id ของมัน แบบเดียวกับ _removedIds
[Serializable]
public class TransitionOverride
{
    [SerializeField] private string _transitionId;
    [SerializeField] private TransitionTableSO.ConditionGroupUsage[] _conditionGroups =
        Array.Empty<TransitionTableSO.ConditionGroupUsage>();

    public string TransitionId => _transitionId;
    public TransitionTableSO.ConditionGroupUsage[] ConditionGroups => _conditionGroups;
}

// Variant: transition จาก Base ตัวนี้ไปจบที่ state อื่นแทน (From / เงื่อนไข ยังตาม Base)
// แยกจาก TransitionOverride: ย้ายปลายแล้ว Base แก้เงื่อนไข เส้นนี้ก็ยังได้ตาม (ถ้าไม่ได้ override เงื่อนไขไว้)
[Serializable]
public class TransitionRetarget
{
    [SerializeField] private string _transitionId;
    [SerializeField] private StateSO _toState;

    public string TransitionId => _transitionId;
    public StateSO ToState => _toState;
}
