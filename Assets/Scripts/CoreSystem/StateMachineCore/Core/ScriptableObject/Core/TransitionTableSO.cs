using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yuki.Learning.StateMachine.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "New_TransitionTable",
        menuName = "YUKI Learning State Machine/StateMachineList/Transition Table")]
    public class TransitionTableSO : ScriptableObject
    {
        [SerializeField]
        private StateSO _initialState;

        [SerializeField]
        private AnyTransitionItem[] _anyTransitions =
            Array.Empty<AnyTransitionItem>();

        [SerializeField]
        private TransitionItem[] _transitions =
            Array.Empty<TransitionItem>();

        // ---------- Variant ----------
        // ตั้ง _base แล้ว table นี้คือ Variant: ใช้ทุกอย่างของ Base แล้วจดเฉพาะที่ต่าง (TableVariantData.cs)
        // ไม่มี _base = table ธรรมดา ทุก list ข้างล่างว่าง

        [SerializeField]
        private TransitionTableSO _base;

        // ค่าใน field ของ action / condition ที่ table นี้เปลี่ยน
        [SerializeField]
        private FieldOverride[] _fieldOverrides =
            Array.Empty<FieldOverride>();

        // Id ของ transition จาก Base ที่ table นี้ไม่เอา
        [SerializeField]
        private string[] _removedIds =
            Array.Empty<string>();

        // transition จาก Base ที่ table นี้ใช้เงื่อนไขของตัวเองแทน (แก้จาก graph: คลิกเส้นของ Base)
        [SerializeField]
        private TransitionOverride[] _transitionOverrides =
            Array.Empty<TransitionOverride>();

        // condition ทีละตัวบน transition จาก Base ที่ table นี้ใช้ตัวอื่นแทน (แก้จาก Inspector ของเส้น)
        [SerializeField]
        private ConditionReplace[] _conditionReplaces =
            Array.Empty<ConditionReplace>();

        // transition จาก Base ที่ table นี้ให้ไปจบที่ state อื่น (แก้จาก graph: ลากเส้นของ Base ไป state อื่น)
        [SerializeField]
        private TransitionRetarget[] _transitionRetargets =
            Array.Empty<TransitionRetarget>();

        // action ของ state ที่ table นี้ลบ / เพิ่ม และลำดับที่จัดใหม่ (แก้จาก graph: คลิก state ของ Base)
        [SerializeField]
        private ActionChange[] _actionChanges =
            Array.Empty<ActionChange>();

        [SerializeField]
        private ActionOrder[] _actionOrders =
            Array.Empty<ActionOrder>();

        [SerializeField, HideInInspector]
        private ExposedParametersSO _exposedParameters;
        public ExposedParametersSO ExposedParameters => _exposedParameters;

        // ---------- สิ่งที่ table นี้ใช้จริง (ของ Base + ของตัวเอง) ----------
        // ทุกตัวเรียงแบบเดียวกัน: ของ Base ก่อน แล้วต่อด้วยของตัวเอง
        // ของตัวเองมาทีหลัง จึงทับของ Base ได้ (เช่น Field Override ตัวเดียวกัน)

        public StateSO EffectiveInitialState
        {
            get
            {
                // ตั้งเองไว้ → ใช้ของตัวเอง
                if (_initialState != null)
                {
                    return _initialState;
                }

                // ไม่ได้ตั้ง แต่มี Base → ถาม Base
                if (_base != null)
                {
                    return _base.EffectiveInitialState;
                }

                return null;
            }
        }

        public IEnumerable<TransitionItem> EffectiveTransitions
        {
            get
            {
                if (_base != null)
                {
                    foreach (TransitionItem baseTransition in _base.EffectiveTransitions)
                    {
                        // เส้นของ Base ที่ table นี้ลบทิ้ง ไม่เอา
                        if (IsRemoved(baseTransition.Id))
                        {
                            continue;
                        }

                        // TransitionItem เป็น struct: ตัวแปรนี้เป็นสำเนา แก้แล้วไม่กระทบของ Base
                        TransitionItem transition = baseTransition;

                        // ทั้งชุด (override) หรือทีละตัว (replace) — มีทั้งชุดแล้ว ไม่แทนทีละตัวอีก
                        ConditionGroupUsage[] overriddenGroups = OverriddenConditionsOf(transition.Id);
                        transition.ConditionGroups = overriddenGroups ??
                            ReplacedConditions(transition.Id, transition.ConditionGroups);

                        StateSO retargetedTo = RetargetOf(transition.Id);
                        if (retargetedTo != null)
                        {
                            transition.ToState = retargetedTo;
                        }

                        yield return transition;
                    }
                }

                foreach (TransitionItem transition in _transitions)
                {
                    yield return transition;
                }
            }
        }

        public IEnumerable<AnyTransitionItem> EffectiveAnyTransitions
        {
            get
            {
                if (_base != null)
                {
                    foreach (AnyTransitionItem baseTransition in _base.EffectiveAnyTransitions)
                    {
                        if (IsRemoved(baseTransition.Id))
                        {
                            continue;
                        }

                        AnyTransitionItem transition = baseTransition;

                        ConditionGroupUsage[] overriddenGroups = OverriddenConditionsOf(transition.Id);
                        transition.ConditionGroups = overriddenGroups ??
                            ReplacedConditions(transition.Id, transition.ConditionGroups);

                        StateSO retargetedTo = RetargetOf(transition.Id);
                        if (retargetedTo != null)
                        {
                            transition.ToState = retargetedTo;
                        }

                        yield return transition;
                    }
                }

                foreach (AnyTransitionItem transition in _anyTransitions)
                {
                    yield return transition;
                }
            }
        }

        public IEnumerable<FieldOverride> EffectiveFieldOverrides
        {
            get
            {
                if (_base != null)
                {
                    foreach (FieldOverride fieldOverride in _base.EffectiveFieldOverrides)
                    {
                        yield return fieldOverride;
                    }
                }

                foreach (FieldOverride fieldOverride in _fieldOverrides)
                {
                    yield return fieldOverride;
                }
            }
        }

        public IEnumerable<ActionChange> EffectiveActionChanges
        {
            get
            {
                if (_base != null)
                {
                    foreach (ActionChange actionChange in _base.EffectiveActionChanges)
                    {
                        yield return actionChange;
                    }
                }

                foreach (ActionChange actionChange in _actionChanges)
                {
                    yield return actionChange;
                }
            }
        }

        public IEnumerable<ActionOrder> EffectiveActionOrders
        {
            get
            {
                if (_base != null)
                {
                    foreach (ActionOrder actionOrder in _base.EffectiveActionOrders)
                    {
                        yield return actionOrder;
                    }
                }

                foreach (ActionOrder actionOrder in _actionOrders)
                {
                    yield return actionOrder;
                }
            }
        }

        // เงื่อนไขที่ table นี้ใช้แทนของ transition นี้ หรือ null ถ้าใช้ของ Base ตามเดิม
        private ConditionGroupUsage[] OverriddenConditionsOf(string transitionId)
        {
            foreach (TransitionOverride transitionOverride in _transitionOverrides)
            {
                if (transitionOverride.TransitionId == transitionId)
                {
                    return transitionOverride.ConditionGroups;
                }
            }

            return null;
        }

        // เงื่อนไขของ Base หลังแทนตัวที่ table นี้แทนไว้ (สำเนาใหม่ ของ Base ไม่ถูกแก้)
        // ไม่มีอะไรแทนบนเส้นนี้ → คืน groups เดิม
        private ConditionGroupUsage[] ReplacedConditions(string transitionId, ConditionGroupUsage[] groups)
        {
            if (groups == null || !HasReplaces(transitionId))
            {
                return groups;
            }

            var result = new ConditionGroupUsage[groups.Length];

            for (int g = 0; g < groups.Length; g++)
            {
                ConditionUsage[] conditions = groups[g].Conditions ?? Array.Empty<ConditionUsage>();
                var copy = new List<ConditionUsage>(conditions.Length);

                foreach (ConditionUsage condition in conditions)
                {
                    // ลบบนเส้นนี้ → ไม่ใส่ (กลุ่มว่างไม่ผ่าน ทาง OR นั้นเลยปิดไปเอง ไม่ใช่ผ่านตลอด)
                    if (IsConditionRemoved(transitionId, condition.Condition))
                    {
                        continue;
                    }

                    ConditionUsage used = condition;
                    StateConditionSO replacement = ReplacementOf(transitionId, condition.Condition);
                    if (replacement != null)
                    {
                        used.Condition = replacement;
                    }

                    copy.Add(used);
                }

                result[g].Conditions = copy.ToArray();
            }

            return result;
        }

        private bool IsConditionRemoved(string transitionId, StateConditionSO condition)
        {
            if (condition == null)
            {
                return false;
            }

            foreach (ConditionReplace replace in _conditionReplaces)
            {
                if (replace.Remove && replace.TransitionId == transitionId && replace.From == condition)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasReplaces(string transitionId)
        {
            foreach (ConditionReplace replace in _conditionReplaces)
            {
                if (replace.TransitionId == transitionId)
                {
                    return true;
                }
            }

            return false;
        }

        // ตัวที่ใช้แทน condition นี้บนเส้นนี้ หรือ null
        private StateConditionSO ReplacementOf(string transitionId, StateConditionSO condition)
        {
            if (condition == null)
            {
                return null;
            }

            foreach (ConditionReplace replace in _conditionReplaces)
            {
                if (!replace.Remove && replace.TransitionId == transitionId && replace.From == condition && replace.To != null)
                {
                    return replace.To;
                }
            }

            return null;
        }

        // state ที่ table นี้ให้ transition นี้ไปจบแทน หรือ null ถ้าไปที่เดิมตาม Base
        private StateSO RetargetOf(string transitionId)
        {
            foreach (TransitionRetarget retarget in _transitionRetargets)
            {
                if (retarget.TransitionId == transitionId)
                {
                    return retarget.ToState;
                }
            }

            return null;
        }

        private bool IsRemoved(string transitionId)
        {
            foreach (string removedId in _removedIds)
            {
                if (removedId == transitionId)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        // แจก ID ให้ transition ที่ยังไม่มี หรือ ID ซ้ำ (Unity copy ID มาด้วยตอน duplicate แถว)
        // Unity เรียกเองตอนโหลด asset และตอนแก้ค่าใน Inspector
        private void OnValidate()
        {
            var usedIds = new HashSet<string>();
            bool changed = false;

            for (int i = 0; i < _transitions.Length; i++)
            {
                if (NeedsNewId(_transitions[i].Id, usedIds))
                {
                    _transitions[i].Id = Guid.NewGuid().ToString();
                    changed = true;
                }

                usedIds.Add(_transitions[i].Id);
            }

            for (int i = 0; i < _anyTransitions.Length; i++)
            {
                if (NeedsNewId(_anyTransitions[i].Id, usedIds))
                {
                    _anyTransitions[i].Id = Guid.NewGuid().ToString();
                    changed = true;
                }

                usedIds.Add(_anyTransitions[i].Id);
            }

            // บอก Unity ว่าต้องเซฟ ไม่งั้น ID ที่สุ่มไว้หายตอนปิด แล้วสุ่มใหม่ทุกครั้งที่เปิด
            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        private static bool NeedsNewId(string id, HashSet<string> usedIds)
        {
            bool hasNoId = string.IsNullOrEmpty(id);
            bool isDuplicate = usedIds.Contains(id);
            return hasNoId || isDuplicate;
        }
#endif

        // Graph editor layout only (StateMachineGraphWindow). Not used at runtime.
        [SerializeField, HideInInspector]
        private NodePosition[] _nodePositions =
            Array.Empty<NodePosition>();

        [SerializeField, HideInInspector]
        private Vector2 _entryNodePosition = new Vector2(0f, 0f);

        [SerializeField, HideInInspector]
        private Vector2 _anyStateNodePosition = new Vector2(0f, 120f);

        [SerializeField, HideInInspector]
        private ContentsFolderEntry[] _contentsFolders =
            Array.Empty<ContentsFolderEntry>();

        [SerializeField, HideInInspector]
        private ContentsFolderPath[] _contentsFolderPaths =
            Array.Empty<ContentsFolderPath>();

        // A variant's own name for a Base's state, shown on its node in this table's graph only.
        // The StateSO keeps its name (Inspector, Project window, Play Mode debug still show that one).
        [SerializeField, HideInInspector]
        private StateDisplayName[] _stateDisplayNames =
            Array.Empty<StateDisplayName>();

        // A Base's exposed parameters (by name) this variant hides from its Parameters panel, like a
        // removed action. Only hides them: the Base's value is used, nothing is unregistered at runtime.
        [SerializeField, HideInInspector]
        private string[] _removedParameters =
            Array.Empty<string>();

        public State CreateInitialState(StateMachine stateMachine)
        {
            return CreateInitialState(stateMachine, out _);
        }
        
        // inheritedOverrides: Field Override ของเครื่องแม่ (sub-state machine ส่งลงมา)
        // ใส่ไว้ก่อนของ table นี้ ของ table นี้เลยทับของแม่ได้ (sub variant เจาะจงกว่า)
        public State CreateInitialState(
            StateMachine stateMachine,
            out IReadOnlyDictionary<StateSO, State> allStates,
            IEnumerable<FieldOverride> inheritedOverrides = null)
        {
            if (stateMachine == null)
            {
                throw new ArgumentNullException(
                    nameof(stateMachine));
            }

            StateSO initialState = EffectiveInitialState;

            if (initialState == null)
            {
                throw new InvalidOperationException(
                    $"Transition table {name} does not have an initial state.");
            }

            RegisterParameters(stateMachine);

            // ตัวตอบว่า "action / condition / action list ไหนใช้จริง" ของ state machine นี้
            // ต้องสร้างหลัง RegisterParameters เพราะ Instance override ต้องใช้ blackboard ที่ register แล้ว
            var fieldOverrides = new List<FieldOverride>();
            if (inheritedOverrides != null)
            {
                fieldOverrides.AddRange(inheritedOverrides);
            }

            fieldOverrides.AddRange(EffectiveFieldOverrides);

            stateMachine.Resolver = new ElementResolver(
                fieldOverrides,
                EffectiveActionChanges,
                EffectiveActionOrders,
                stateMachine.Owner.GetComponent<StateMachineParameters>());

            var runtimeStates =
                new Dictionary<StateSO, State>();

            var transitionLists =
                new Dictionary<State, List<StateTransition>>();

            var runtimeConditions =
                new Dictionary<StateConditionSO, Condition>();

            State initialRuntimeState = GetOrCreateState(
                initialState,
                stateMachine,
                runtimeStates,
                transitionLists);

            BuildLocalTransitions(
                stateMachine,
                runtimeStates,
                transitionLists,
                runtimeConditions);

            foreach (
                KeyValuePair<State, List<StateTransition>> pair
                in transitionLists)
            {
                pair.Key.SetTransitions(
                    pair.Value.ToArray());
            }

            StateTransition[] runtimeAnyTransitions =
                BuildAnyTransitions(
                    stateMachine,
                    runtimeStates,
                    transitionLists,
                    runtimeConditions);

            stateMachine.SetAnyTransitions(
                runtimeAnyTransitions);

            allStates = runtimeStates;
            return initialRuntimeState;
        }

        // blackboard ของ table นี้ และของ Base ทุกชั้น (Base ก่อน)
        // ตัวละครจึง override parameter ของ Base ได้เหมือนของ table นี้
        private void RegisterParameters(StateMachine stateMachine)
        {
            if (_base != null)
            {
                _base.RegisterParameters(stateMachine);
            }

            StateMachineParameters.Register(stateMachine, _exposedParameters);
        }

        private void BuildLocalTransitions(
            StateMachine stateMachine,
            Dictionary<StateSO, State> runtimeStates,
            Dictionary<State, List<StateTransition>> transitionLists,
            Dictionary<StateConditionSO, Condition> runtimeConditions)
        {
            foreach (TransitionItem item in EffectiveTransitions)
            {
                State fromState = GetOrCreateState(
                    item.FromState,
                    stateMachine,
                    runtimeStates,
                    transitionLists);

                State toState = GetOrCreateState(
                    item.ToState,
                    stateMachine,
                    runtimeStates,
                    transitionLists);

                StateCondition[][] conditionGroups =
                    CreateConditionGroups(
                        item.ConditionGroups,
                        runtimeConditions,
                        stateMachine);

                transitionLists[fromState].Add(
                    new StateTransition(
                        toState,
                        conditionGroups));
            }
        }

        private StateTransition[] BuildAnyTransitions(
            StateMachine stateMachine,
            Dictionary<StateSO, State> runtimeStates,
            Dictionary<State, List<StateTransition>> transitionLists,
            Dictionary<StateConditionSO, Condition> runtimeConditions)
        {
            var runtimeTransitions = new List<StateTransition>();

            foreach (AnyTransitionItem item in EffectiveAnyTransitions)
            {
                State toState = GetOrCreateState(
                    item.ToState,
                    stateMachine,
                    runtimeStates,
                    transitionLists);

                StateCondition[][] conditionGroups =
                    CreateConditionGroups(
                        item.ConditionGroups,
                        runtimeConditions,
                        stateMachine);

                runtimeTransitions.Add(
                    new StateTransition(
                        toState,
                        conditionGroups));
            }

            return runtimeTransitions.ToArray();
        }

        private StateCondition[][] CreateConditionGroups(
            ConditionGroupUsage[] groupUsages,
            Dictionary<StateConditionSO, Condition> runtimeConditions,
            StateMachine stateMachine)
        {
            ConditionGroupUsage[] usages =
                groupUsages ??
                Array.Empty<ConditionGroupUsage>();

            var runtimeGroups =
                new StateCondition[usages.Length][];

            for (int groupIndex = 0;
                groupIndex < usages.Length;
                groupIndex++)
            {
                ConditionUsage[] conditionUsages =
                    usages[groupIndex].Conditions ??
                    Array.Empty<ConditionUsage>();

                var runtimeGroup =
                    new StateCondition[
                        conditionUsages.Length];

                for (int conditionIndex = 0;
                    conditionIndex < conditionUsages.Length;
                    conditionIndex++)
                {
                    ConditionUsage usage =
                        conditionUsages[conditionIndex];

                    Condition runtimeCondition =
                        GetOrCreateCondition(
                            usage.Condition,
                            runtimeConditions,
                            stateMachine);

                    bool expectedResult =
                        usage.ExpectedResult ==
                        Result.True;

                    runtimeGroup[conditionIndex] =
                        new StateCondition(
                            runtimeCondition,
                            expectedResult);
                }

                runtimeGroups[groupIndex] =
                    runtimeGroup;
            }

            return runtimeGroups;
        }

        private State GetOrCreateState(
            StateSO stateSO,
            StateMachine stateMachine,
            Dictionary<StateSO, State> runtimeStates,
            Dictionary<State, List<StateTransition>> transitionLists)
        {
            if (stateSO == null)
            {
                throw new InvalidOperationException(
                    $"Transition table {name} contains a transition with a missing state.");
            }

            if (runtimeStates.TryGetValue(
                stateSO,
                out State existingState))
            {
                return existingState;
            }

            State newState =
                stateSO.CreateState(stateMachine);

            if (newState == null)
            {
                throw new InvalidOperationException(
                    $"State {stateSO.name} returned a null runtime state.");
            }

            runtimeStates.Add(
                stateSO,
                newState);

            transitionLists.Add(
                newState,
                new List<StateTransition>());

            return newState;
        }

        private Condition GetOrCreateCondition(
            StateConditionSO conditionSO,
            Dictionary<StateConditionSO, Condition> runtimeConditions,
            StateMachine stateMachine)
        {
            if (conditionSO == null)
            {
                throw new InvalidOperationException(
                    $"Transition table {name} contains a missing condition.");
            }

            if (runtimeConditions.TryGetValue(
                conditionSO,
                out Condition existingCondition))
            {
                return existingCondition;
            }

            // Resolve: ตัวที่มีค่าของ Variant / ตัวละครใส่ไว้แล้ว (หรือตัวเดิมถ้าไม่มีใคร override)
            Condition newCondition =
                stateMachine.Resolver.Resolve(conditionSO).CreateCondition();

            if (newCondition == null)
            {
                throw new InvalidOperationException(
                    $"Condition {conditionSO.name} returned a null runtime condition.");
            }

            // Initialize the runtime Condition.
            newCondition.DebugName = conditionSO.name;
            newCondition.Awake(stateMachine);

            // StateMachine owns this runtime Condition
            // and will dispose it when the controller is destroyed.
            stateMachine.RegisterCondition(
                newCondition);

            runtimeConditions.Add(
                conditionSO,
                newCondition);

            return newCondition;
        }

        [Serializable]
        public struct AnyTransitionItem
        {
            public StateSO ToState;
            public ConditionGroupUsage[] ConditionGroups;
            public string Id;
        }

        [Serializable]
        public struct TransitionItem
        {
            public StateSO FromState;
            public ConditionGroupUsage[] ConditionGroups;
            public StateSO ToState;
            public string Id;
        }

        public enum Result
        {
            True,
            False
        }

        [Serializable]
        public struct ConditionUsage
        {
            public StateConditionSO Condition;
            public Result ExpectedResult;
        }

        [Serializable]
        public struct ConditionGroupUsage
        {
            public ConditionUsage[] Conditions;
        }

        [Serializable]
        public struct NodePosition
        {
            public StateSO State;
            public Vector2 Position;
        }
        
        [Serializable]
        public struct ContentsFolderEntry
        {
            public ScriptableObject Item;   // a State, Action or Condition this table uses
            public string Folder;           // "Combat", or "Combat/Melee" for a folder inside a folder
        }

        [Serializable]
        public struct StateDisplayName
        {
            public StateSO State;
            public string Name;
        }

        [Serializable]
        public struct ContentsFolderPath
        {
            public string Group;   // "States", "Actions" or "Conditions"
            public string Path;    // "Combat", or "Combat/Melee" for a folder inside a folder
        }

    }
}