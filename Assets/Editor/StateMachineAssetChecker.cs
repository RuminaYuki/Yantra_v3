using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// ตัวช่วยตรวจอย่างเดียว ไม่แก้ไฟล์ใคร — ต้องวางไว้ในโฟลเดอร์ชื่อ Editor เท่านั้น
public static class StateMachineAssetChecker
{
    private static HashSet<string> _playerAssemblies;

    [MenuItem("Tools/Yantra/ตรวจ State Machine ก่อน Build")]
    private static void Check()
    {
        _playerAssemblies = new HashSet<string>(
            CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(a => a.name));

        var report = new StringBuilder();
        int problems = 0;

        // ไล่ตามตาราง จะได้รู้ว่าปัญหาเป็นของผีตัวไหน และข้าม state ที่ไม่มีใครใช้
        foreach (string guid in AssetDatabase.FindAssets("t:TransitionTableSO"))
        {
            var table = AssetDatabase.LoadAssetAtPath<TransitionTableSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (table == null) continue;

            var lines = new StringBuilder();

            foreach (var (state, isAnyTarget) in CollectStates(table))
            {
                foreach (string problem in CheckState(state))
                {
                    lines.AppendLine($"  - {state.name}{(isAnyTarget ? " [ปลายทาง Any]" : "")} → {problem}");
                    problems++;
                }
            }

            if (lines.Length > 0)
                report.AppendLine($"■ {table.name}").Append(lines);
        }

        string result = $"[SM Check] เจอ {problems} จุด\n{report}";
        EditorGUIUtility.systemCopyBuffer = result;

        if (problems == 0) Debug.Log("[SM Check] ไม่เจอปัญหา ✓");
        else Debug.LogWarning(result + "\n(copy รายงานไว้ให้แล้ว กด Ctrl+V วางได้เลย)");
    }

    private static IEnumerable<(StateSO state, bool isAnyTarget)> CollectStates(TransitionTableSO table)
    {
        var states = new Dictionary<StateSO, bool>();
        var so = new SerializedObject(table);

        Add(states, so.FindProperty("_initialState"), false);

        SerializedProperty any = so.FindProperty("_anyTransitions");
        for (int i = 0; any != null && i < any.arraySize; i++)
            Add(states, any.GetArrayElementAtIndex(i).FindPropertyRelative("ToState"), true);

        SerializedProperty local = so.FindProperty("_transitions");
        for (int i = 0; local != null && i < local.arraySize; i++)
        {
            SerializedProperty item = local.GetArrayElementAtIndex(i);
            Add(states, item.FindPropertyRelative("FromState"), false);
            Add(states, item.FindPropertyRelative("ToState"), false);
        }

        return states.Select(p => (p.Key, p.Value));
    }

    private static void Add(Dictionary<StateSO, bool> states, SerializedProperty prop, bool isAnyTarget)
    {
        if (prop == null || !(prop.objectReferenceValue is StateSO state)) return;
        states[state] = states.TryGetValue(state, out bool was) ? was || isAnyTarget : isAnyTarget;
    }

    private static IEnumerable<string> CheckState(StateSO state)
    {
        SerializedProperty actions = new SerializedObject(state).FindProperty("_actions");
        if (actions == null) yield break;

        for (int i = 0; i < actions.arraySize; i++)
        {
            string problem = FindProblem(actions.GetArrayElementAtIndex(i).objectReferenceValue);
            if (problem != null) yield return $"Actions[{i}] {problem}";
        }
    }

    private static string FindProblem(Object action)
    {
        if (action == null)
            return "ช่องว่าง หรือไฟล์ที่อ้างถึงหายไปแล้ว";

        MonoScript script = MonoScript.FromScriptableObject((ScriptableObject)action);
        if (script == null)
            return $"'{action.name}' หาไฟล์สคริปต์ไม่เจอ";

        System.Type type = action.GetType();
        string path = AssetDatabase.GetAssetPath(script);

        if (script.name != type.Name)
            return $"'{action.name}' ชื่อไฟล์ {script.name}.cs ไม่ตรงกับชื่อคลาส {type.Name} ({path})";

        if (!_playerAssemblies.Contains(type.Assembly.GetName().Name))
            return $"'{action.name}' สคริปต์ไม่ถูกใส่ใน build — อยู่ในโฟลเดอร์ Editor ({path})";

        if (IsClassWrappedInEditorOnly(script.text, type.Name))
            return $"'{action.name}' ทั้งคลาสอยู่ใน #if UNITY_EDITOR — ใน build ไม่มีคลาสนี้ ({path})";

        return null;
    }

    // #if UNITY_EDITOR ที่เปิดไว้ก่อนประกาศคลาสแล้วยังไม่ปิด = ครอบทั้งคลาส
    // (แบบครอบแค่ using UnityEditor ข้างบน แล้วปิด #endif ก่อนถึงคลาส ไม่นับ)
    private static bool IsClassWrappedInEditorOnly(string code, string className)
    {
        int classIndex = code.IndexOf("class " + className);
        if (classIndex < 0) return false;

        string before = code.Substring(0, classIndex);
        int ifIndex = before.LastIndexOf("#if UNITY_EDITOR");
        return ifIndex >= 0 && before.IndexOf("#endif", ifIndex) < 0;
    }
}