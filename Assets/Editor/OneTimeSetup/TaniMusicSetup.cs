#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

/// <summary>
/// เพลงบอสตานี — ใส่ action "Music Override" ลง Tanee_TransitionTable — กดซ้ำได้ ไม่ใส่ซ้ำ
///   เริ่มสู้ (state ที่ NotActive ของ BasephaseSub วิ่งไปตอนผู้เล่นเข้าเขต) → เปลี่ยนเป็น BGM_Tani
///   Death_State → คืนเพลงเดิม
/// ตานีโดนลบ / ปิดไปก่อน = BattleMusicController ถอดเพลงให้เองอยู่แล้ว
/// </summary>
public static class TaniMusicSetup
{
    private const string TableAssetName = "Tanee_TransitionTable";
    private const string StartTableName = "BasephaseSub_TransitionTable";
    private const string IdleStateName = "NotActive";
    private const string DeathStateName = "Death_State";
    private const string MusicName = "BGM_Tani";
    private const string MusicFolderHint = "BGM_Battle";

    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Setup Tani Boss Music (เพลงบอสตานี)")]
    private static void Run()
    {
        TransitionTableSO root = FindAsset<TransitionTableSO>(TableAssetName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Tani Music", $"หา {TableAssetName} ไม่เจอ", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(root);
        Object[] subs = AssetDatabase.LoadAllAssetsAtPath(path);
        var report = new StringBuilder();

        SoundID music = FindOrCreateMusicId(out bool created);
        if (created) report.AppendLine($"สร้าง SoundID {MusicName} แล้ว");

        // หา state ตอนเริ่มสู้ = ปลายทางของ NotActive ใน BasephaseSub
        List<StateSO> startStates = FindStartStates(subs);
        if (startStates.Count == 0)
            report.AppendLine($"✗ หา state เริ่มสู้ (ต่อจาก {IdleStateName} ใน {StartTableName}) ไม่เจอ");

        var startAction = GetOrCreate(root, subs, "Tani_Music_Start", MusicOverrideActionSO.Mode.Push, music);
        foreach (StateSO s in startStates)
            report.AppendLine($"♪ {s.name} ← เปลี่ยนเพลง {MusicName}{(AddToState(s, startAction) ? "" : " — มีอยู่แล้ว")}");

        var stopAction = GetOrCreate(root, subs, "Tani_Music_Stop", MusicOverrideActionSO.Mode.Pop, null);
        int deaths = 0;
        foreach (Object o in subs)
        {
            if (!(o is StateSO s) || s.name != DeathStateName) continue;
            deaths++;
            report.AppendLine($"♪ {s.name} ← คืนเพลงเดิม{(AddToState(s, stopAction) ? "" : " — มีอยู่แล้ว")}");
        }
        if (deaths == 0) report.AppendLine($"✗ ไม่เจอ {DeathStateName}");

        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);

        if (!InLibrary(music))
            report.AppendLine($"\nยังไม่มี {MusicName} ใน SoundLibrary ไหนเลย — เพิ่มแล้วใส่ไฟล์เพลง (Mixer = BGM)");

        Debug.Log($"[TaniMusic]\n{report}", root);
        EditorUtility.DisplayDialog("Tani Music", report.ToString(), "OK");
        Selection.activeObject = root;
    }

    private static List<StateSO> FindStartStates(Object[] subs)
    {
        var result = new List<StateSO>();
        TransitionTableSO startTable = null;
        foreach (Object o in subs)
            if (o is TransitionTableSO t && t.name == StartTableName) { startTable = t; break; }
        if (startTable == null) return result;

        var so = new SerializedObject(startTable);
        SerializedProperty list = so.FindProperty("_transitions");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty item = list.GetArrayElementAtIndex(i);
            var from = item.FindPropertyRelative("FromState").objectReferenceValue as StateSO;
            var to = item.FindPropertyRelative("ToState").objectReferenceValue as StateSO;
            if (from != null && to != null && from.name == IdleStateName && !result.Contains(to))
                result.Add(to);
        }
        return result;
    }

    private static MusicOverrideActionSO GetOrCreate(TransitionTableSO root, Object[] subs, string name,
                                                     MusicOverrideActionSO.Mode mode, SoundID music)
    {
        MusicOverrideActionSO action = null;
        foreach (Object o in subs)
            if (o is MusicOverrideActionSO a && a.name == name) { action = a; break; }

        if (action == null)
        {
            action = ScriptableObject.CreateInstance<MusicOverrideActionSO>();
            action.name = name;
            AssetDatabase.AddObjectToAsset(action, root);
        }

        var so = new SerializedObject(action);
        so.FindProperty("_mode").enumValueIndex = (int)mode;
        so.FindProperty("_music").objectReferenceValue = music;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(action);
        return action;
    }

    private static bool AddToState(StateSO state, StateActionSO action)
    {
        var so = new SerializedObject(state);
        SerializedProperty list = so.FindProperty("_actions");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == action) return false;

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = action;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(state);
        return true;
    }

    private static SoundID FindOrCreateMusicId(out bool created)
    {
        created = false;
        SoundID found = FindAsset<SoundID>(MusicName);
        if (found != null) return found;

        string folder = "Assets";
        SoundID hint = FindAsset<SoundID>(MusicFolderHint);
        if (hint != null) folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(hint)).Replace('\\', '/');

        var id = ScriptableObject.CreateInstance<SoundID>();
        AssetDatabase.CreateAsset(id, $"{folder}/{MusicName}.asset");
        created = true;
        return id;
    }

    // อยู่ใน SoundLibrary ตัวไหนก็ได้
    private static bool InLibrary(SoundID id)
    {
        if (id == null) return false;
        foreach (string guid in AssetDatabase.FindAssets("t:SoundLibrary"))
        {
            var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(AssetDatabase.GUIDToAssetPath(guid));
            if (lib == null || lib.sounds == null) continue;
            foreach (SoundData s in lib.sounds)
                if (s != null && s.id == id) return true;
        }
        return false;
    }

    private static T FindAsset<T>(string exactName) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"{exactName} t:{typeof(T).Name}"))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name == exactName) return asset;
        }
        return null;
    }
}
#endif
