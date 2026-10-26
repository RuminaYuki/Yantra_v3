using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// ตอบคำถาม "state machine ตัวนี้ใช้อะไรจริง" มี 1 ตัวต่อ 1 state machine
//   - action list ของ state:  action ของ state → Variant ลบ / เพิ่ม → Variant จัดลำดับ
//   - ค่าใน action / condition: SO เดิม → Variant override → Instance override
public class ElementResolver
{
    private readonly List<FieldOverride> _variantOverrides;
    private readonly List<ActionChange> _actionChanges;
    private readonly List<ActionOrder> _actionOrders;

    // null ได้ (ตัวละครไม่มี component StateMachineParameters)
    private readonly StateMachineParameters _instance;

    // ส่งต่อให้ sub-state machine (SubStateMachineAction) เครื่องลูกจะได้ใช้ค่าของ Variant ตัวแม่ด้วย
    public IReadOnlyList<FieldOverride> VariantOverrides => _variantOverrides;

    // ถามซ้ำด้วย SO ตัวเดิม → ได้ตัวเดิม
    private readonly Dictionary<StateElementSO, StateElementSO> _resolved =
        new Dictionary<StateElementSO, StateElementSO>();

    // ตัว copy ที่สร้างขึ้น (ไว้ลบตอน Dispose)
    private readonly List<StateElementSO> _copies = new List<StateElementSO>();

    public ElementResolver(
        IEnumerable<FieldOverride> variantOverrides,
        IEnumerable<ActionChange> actionChanges,
        IEnumerable<ActionOrder> actionOrders,
        StateMachineParameters instance)
    {
        _variantOverrides = new List<FieldOverride>(variantOverrides);
        _actionChanges = new List<ActionChange>(actionChanges);
        _actionOrders = new List<ActionOrder>(actionOrders);
        _instance = instance;
    }

    // ---------- action list ของ state ----------

    // action ที่ state นี้ทำจริงใน table นี้
    public List<StateActionSO> ActionsFor(StateSO state, IEnumerable<StateActionSO> stateActions)
    {
        return BuildActionList(state, stateActions, _actionChanges, _actionOrders);
    }

    // static เพราะ graph editor ใช้คำนวณแบบเดียวกันเพื่อแสดงผล
    public static List<StateActionSO> BuildActionList(
        StateSO state,
        IEnumerable<StateActionSO> stateActions,
        IEnumerable<ActionChange> changes,
        IEnumerable<ActionOrder> orders)
    {
        var actions = new List<StateActionSO>();

        // ตัวเดิม → ตัวที่แทน (Replace) ลำดับที่จัดไว้ (ActionOrder) ที่มีตัวเดิมอยู่ จะได้ใช้ตัวที่แทนตรงนั้น
        var replaced = new Dictionary<StateActionSO, StateActionSO>();

        foreach (StateActionSO action in stateActions)
        {
            if (action != null)
            {
                actions.Add(action);
            }
        }

        // ลบ / เพิ่ม ทีละอันตามลำดับ (Base ก่อน Variant ทีหลัง) Variant จึงลบ action ที่ Base เพิ่มไว้ได้
        foreach (ActionChange change in changes)
        {
            if (change.State != state || change.Action == null)
            {
                continue;
            }

            if (change.Kind == ActionChange.ChangeKind.Remove)
            {
                actions.Remove(change.Action);
            }
            else if (change.Kind == ActionChange.ChangeKind.Add)
            {
                actions.Add(change.Action);
            }
            else if (change.Kind == ActionChange.ChangeKind.Replace && change.With != null)
            {
                // ที่ตำแหน่งเดิม ลำดับของ Base จึงยังตาม Base / ไม่มีตัวเดิมแล้ว (Base ลบทิ้ง) ก็ไม่ทำอะไร
                int index = actions.IndexOf(change.Action);
                if (index >= 0)
                {
                    actions[index] = change.With;
                    replaced[change.Action] = change.With;
                }
            }
        }

        ActionOrder order = LastOrderFor(state, orders);
        if (order != null)
        {
            StateActionSO[] orderedActions = order.Actions
                .Select(action => action != null && replaced.TryGetValue(action, out StateActionSO with) ? with : action)
                .ToArray();
            actions = SortByOrder(actions, orderedActions);
        }

        return actions;
    }

    // ลำดับของ Variant มาทีหลัง จึงชนะลำดับของ Base
    private static ActionOrder LastOrderFor(StateSO state, IEnumerable<ActionOrder> orders)
    {
        ActionOrder found = null;

        foreach (ActionOrder order in orders)
        {
            if (order.State == state)
            {
                found = order;
            }
        }

        return found;
    }

    // ตามลำดับที่จัดไว้ก่อน แล้วต่อด้วยตัวที่ไม่อยู่ในลำดับ (เช่น Base เพิ่มทีหลัง)
    private static List<StateActionSO> SortByOrder(List<StateActionSO> actions, StateActionSO[] order)
    {
        var sorted = new List<StateActionSO>();

        foreach (StateActionSO action in order)
        {
            if (actions.Contains(action) && !sorted.Contains(action))
            {
                sorted.Add(action);
            }
        }

        foreach (StateActionSO action in actions)
        {
            if (!sorted.Contains(action))
            {
                sorted.Add(action);
            }
        }

        return sorted;
    }

    // ---------- ค่าใน action / condition ----------

    public T Resolve<T>(T original) where T : StateElementSO
    {
        if (original == null)
        {
            return null;
        }

        if (_resolved.TryGetValue(original, out StateElementSO alreadyResolved))
        {
            return (T)alreadyResolved;
        }

        T result = original;

        // 1. Variant override (Base ก่อน Variant ทีหลัง → Variant ทับ Base)
        foreach (FieldOverride fieldOverride in _variantOverrides)
        {
            if (fieldOverride.Target != original)
            {
                continue;
            }

            // copy ครั้งแรกที่เจอ override จะได้ไม่เขียนทับ asset ตัวจริง
            if (result == original)
            {
                result = Object.Instantiate(original);
                _copies.Add(result);
            }

            FieldPathUtility.Apply(result, fieldOverride.FieldPath, fieldOverride);
        }

        // 2. Instance override (ทับ Variant) ถ้า Variant ยังไม่ได้ copy มันจะ copy เอง
        if (_instance != null)
        {
            result = _instance.ApplyOverrides(original, result);

            bool instanceMadeCopy = result != original && !_copies.Contains(result);
            if (instanceMadeCopy)
            {
                _copies.Add(result);
            }
        }

        _resolved[original] = result;
        return result;
    }

    // ตัว copy ไม่ใช่ asset Unity ไม่ลบให้เอง → state machine ถูก Dispose ก็ลบทิ้ง
    public void Dispose()
    {
        foreach (StateElementSO copy in _copies)
        {
            Object.Destroy(copy);
        }

        _copies.Clear();
        _resolved.Clear();
    }
}
