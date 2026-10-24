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

// Variant: ลบ / เพิ่ม / แทน action ของ state หนึ่ง เฉพาะ table นี้ (state ของ Base ไม่ถูกแก้)
//   Remove:  ใน State ไม่ทำ Action
//   Add:     ใน State ทำ Action เพิ่ม (ต่อท้าย ถ้าไม่ได้จัดลำดับไว้ใน ActionOrder)
//   Replace: ใน State ทำ With แทน Action ที่ตำแหน่งเดิม (Base ลบ Action ทิ้ง → ไม่มีผล)
[Serializable]
public class ActionChange
{
    // ตัวเลขกำหนดเอง: Unity เซฟเป็นตัวเลข ถ้าลบหรือแทรกชนิดใหม่ ของเก่าจะไม่เปลี่ยนความหมาย
    public enum ChangeKind
    {
        Remove = 1,
        Add = 2,
        Replace = 3
    }

    [SerializeField] private StateSO _state;
    [SerializeField] private ChangeKind _kind;
    [SerializeField] private StateActionSO _action;
    [SerializeField] private StateActionSO _with;   // Replace เท่านั้น

    public StateSO State => _state;
    public ChangeKind Kind => _kind;
    public StateActionSO Action => _action;
    public StateActionSO With => _with;
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

// Variant: บน transition จาก Base ตัวนี้ ใช้ condition _to แทน _from (Expected / ตัวอื่น ยังตาม Base)
// ต่างจาก TransitionOverride ที่เอาไปทั้งชุด: Base แก้หรือเพิ่ม condition อื่นในเส้นนี้ Variant ก็ยังได้ตาม
// อ้างด้วยตัว SO ไม่ใช่ลำดับ Base สลับลำดับก็ยังถูกตัว / ถ้ามี TransitionOverride เส้นเดียวกัน อันนั้นชนะ
// _remove: ไม่เช็ก _from บนเส้นนี้เลย (_to ไม่ใช้) / ลบตัวสุดท้ายในกลุ่ม → กลุ่มว่าง = ไม่ผ่าน (ทาง OR นั้นปิด)
[Serializable]
public class ConditionReplace
{
    [SerializeField] private string _transitionId;
    [SerializeField] private StateConditionSO _from;
    [SerializeField] private StateConditionSO _to;
    [SerializeField] private bool _remove;

    public string TransitionId => _transitionId;
    public StateConditionSO From => _from;
    public StateConditionSO To => _to;
    public bool Remove => _remove;
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
