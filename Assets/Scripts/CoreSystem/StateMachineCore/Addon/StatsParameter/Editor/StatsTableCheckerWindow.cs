using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Editor-only bridge between StatsParameter and the state machine: compares a StatsProfileSO's
// modifiers against what the given TransitionTables actually use. Neither system knows about this
// tool — it reads both through SerializedObject (_modifiers, _target), so no runtime code changes.
public class StatsTableCheckerWindow : EditorWindow
{
    private const string ModifiersField = "_modifiers";
    private const string TargetField = "_target";

    [SerializeField] private StatsProfileSO _profile;
    [SerializeField] private TransitionTableSO[] _tables = Array.Empty<TransitionTableSO>();

    private readonly List<Entry> _results = new List<Entry>();
    private SerializedObject _serializedWindow;
    private int _skippedCount;
    private bool _hasChecked;
    private Vector2 _scroll;

    // Order = display order (problems first).
    private enum Kind { MissingTarget, Orphan, Duplicate, Uncovered, Covered }

    private struct Entry
    {
        public Kind Kind;
        public UnityEngine.Object Subject;
        public UnityEngine.Object Modifier;
    }

    [MenuItem("Tools/State Machine/Check Stats Against Table")]
    private static void Open() => GetWindow<StatsTableCheckerWindow>("Stats vs Table");

    private void OnEnable() => _serializedWindow = new SerializedObject(this);

    private void OnGUI()
    {
        _serializedWindow.Update();
        EditorGUILayout.PropertyField(_serializedWindow.FindProperty(nameof(_profile)));
        EditorGUILayout.PropertyField(_serializedWindow.FindProperty(nameof(_tables)), true);
        _serializedWindow.ApplyModifiedProperties();

        bool canCheck = _profile != null && _tables.Any(t => t != null);
        using (new EditorGUI.DisabledScope(!canCheck))
        {
            if (GUILayout.Button("Check", GUILayout.Height(24)))
                Check();
        }

        if (_hasChecked)
            DrawResults();
    }

    private void Check()
    {
        _results.Clear();
        _skippedCount = 0;
        _hasChecked = true;

        HashSet<ScriptableObject> used = CollectReachable(_tables);
        List<Type> tunableTypes = CollectTunableTypes();
        var covered = new HashSet<UnityEngine.Object>();

        SerializedProperty modifiers = new SerializedObject(_profile).FindProperty(ModifiersField);
        for (int i = 0; i < modifiers.arraySize; i++)
        {
            UnityEngine.Object modifier = modifiers.GetArrayElementAtIndex(i).objectReferenceValue;
            if (modifier == null)
                continue;

            SerializedProperty target = new SerializedObject(modifier).FindProperty(TargetField);
            if (target == null)
            {
                // Header / Component modifiers (they use _anchor, not a state machine asset).
                _skippedCount++;
                continue;
            }

            foreach (UnityEngine.Object targetObject in ReadTargets(target))
            {
                if (targetObject == null)
                    Add(Kind.MissingTarget, modifier, modifier);
                else if (!covered.Add(targetObject))
                    Add(Kind.Duplicate, targetObject, modifier);
                else if (used.Contains(targetObject as ScriptableObject))
                    Add(Kind.Covered, targetObject, modifier);
                else
                    Add(Kind.Orphan, targetObject, modifier);
            }
        }

        foreach (ScriptableObject usedObject in used)
        {
            if (!covered.Contains(usedObject) &&
                tunableTypes.Any(type => type.IsInstanceOfType(usedObject)))
            {
                Add(Kind.Uncovered, usedObject, null);
            }
        }

        _results.Sort((a, b) => a.Kind.CompareTo(b.Kind));
    }

    private void Add(Kind kind, UnityEngine.Object subject, UnityEngine.Object modifier)
    {
        _results.Add(new Entry { Kind = kind, Subject = subject, Modifier = modifier });
    }

    // Walks every ScriptableObject reachable from the tables (states, actions, conditions, and
    // sub-state-machine tables inside actions) without depending on their field names.
    private static HashSet<ScriptableObject> CollectReachable(IEnumerable<TransitionTableSO> tables)
    {
        var visited = new HashSet<ScriptableObject>();
        var pending = new Stack<ScriptableObject>(tables.Where(t => t != null));

        while (pending.Count > 0)
        {
            ScriptableObject current = pending.Pop();
            if (!visited.Add(current))
                continue;

            SerializedProperty iterator = new SerializedObject(current).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                    iterator.objectReferenceValue is ScriptableObject child &&
                    !visited.Contains(child))
                {
                    pending.Push(child);
                }
            }
        }

        return visited;
    }

    // A Condition/Action type counts as "tunable" when some modifier class has a _target of that type.
    private static List<Type> CollectTunableTypes()
    {
        var types = new List<Type>();

        foreach (Type modifierType in TypeCache.GetTypesDerivedFrom<IStatModifier>())
        {
            FieldInfo field = modifierType.GetField(
                TargetField,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                continue;

            Type type = field.FieldType;
            if (type.IsArray)
                type = type.GetElementType();
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                type = type.GetGenericArguments()[0];

            if (typeof(StateConditionSO).IsAssignableFrom(type) ||
                typeof(StateActionSO).IsAssignableFrom(type))
            {
                types.Add(type);
            }
        }

        return types;
    }

    // _target is usually a single reference, but e.g. SetCurrentSpeedModifierSO uses a List.
    private static IEnumerable<UnityEngine.Object> ReadTargets(SerializedProperty target)
    {
        if (target.isArray && target.propertyType == SerializedPropertyType.Generic)
        {
            for (int i = 0; i < target.arraySize; i++)
                yield return target.GetArrayElementAtIndex(i).objectReferenceValue;
        }
        else if (target.propertyType == SerializedPropertyType.ObjectReference)
        {
            yield return target.objectReferenceValue;
        }
    }

    private void DrawResults()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            $"Covered {Count(Kind.Covered)}   Uncovered {Count(Kind.Uncovered)}   " +
            $"Orphan {Count(Kind.Orphan)}   Skipped {_skippedCount} (Header / Component)",
            EditorStyles.miniBoldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (Entry entry in _results)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(Describe(entry), MessageTypeOf(entry.Kind));
                if (entry.Subject != null && GUILayout.Button("Ping", GUILayout.Width(40), GUILayout.Height(38)))
                    EditorGUIUtility.PingObject(entry.Subject);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private int Count(Kind kind) => _results.Count(r => r.Kind == kind);

    private static string Describe(Entry entry)
    {
        string subject = entry.Subject != null ? entry.Subject.name : "(null)";
        string modifier = entry.Modifier != null ? entry.Modifier.name : "";

        switch (entry.Kind)
        {
            case Kind.MissingTarget:
                return $"{modifier}: _target is empty.";
            case Kind.Orphan:
                return $"{subject} ← {modifier}\nNot used by these tables anymore.";
            case Kind.Duplicate:
                return $"{subject} ← {modifier}\nAlready set by another modifier; the later one in the list wins.";
            case Kind.Uncovered:
                return $"{subject}\nIn the table but no modifier. Ignore if it should keep its own value.";
            default:
                return $"{subject} ← {modifier}";
        }
    }

    private static MessageType MessageTypeOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.MissingTarget: return MessageType.Error;
            case Kind.Covered: return MessageType.Info;
            default: return MessageType.Warning;
        }
    }
}
