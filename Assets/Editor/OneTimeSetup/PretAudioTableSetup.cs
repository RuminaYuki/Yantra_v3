#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

/// <summary>
/// ใส่เสียงเตือนของเปรตลงใน PPret_TransitionTable — กดซ้ำได้ ไม่ใส่ซ้ำ
///   EnterStare_State → Pret_Whistle (ครั้งเดียว)     เปรตเห็นเราแล้ว
///   Stare_State      → Pret_Stare_Loop (วนตลอด state) ระหว่างจ้อง
///   Alert_State      → Pret_Charge (ครั้งเดียว)      ท่าง้าง — Alert เล่นท่า Charge อยู่แล้ว
///   Attack_State     → Pret_Scream (ครั้งเดียว)      ตอนพุ่งตบ
///   BackHome_State   → Pret_BackHome (ครั้งเดียว)    ตอนกลับบ้าน
/// เพลง (Music Override):
///   EnterStare_State → เปลี่ยนเป็น BGM_Pret (ค้างไว้)
///   BackHome_State   → คืนเพลงเดิม
/// SoundID ที่ยังไม่มีจะสร้างให้ (ไฟล์เสียงต้องไปใส่ใน Lib_Pret เอง)
/// </summary>
public static class PretAudioTableSetup
{
    private const string TableName = "PPret_TransitionTable";
    private const string SoundIdFolderHint = "Pret_Whistle";   // สร้าง SoundID ใหม่ไว้โฟลเดอร์เดียวกับตัวนี้
    private const string MusicName = "BGM_Pret";
    private const string MusicFolderHint = "BGM_Battle";

    private struct Entry
    {
        public string State, Action, Sound;
        public PlayGhostAudioActionSO.Mode Mode;
        public float Delay, Fade;

        public Entry(string state, string action, PlayGhostAudioActionSO.Mode mode, string sound, float delay = 0f, float fade = 0.5f)
        {
            State = state; Action = action; Mode = mode; Sound = sound; Delay = delay; Fade = fade;
        }
    }

    private static readonly Entry[] Entries =
    {
        new Entry("EnterStare_State", "Pret_EnterStare_Audio", PlayGhostAudioActionSO.Mode.Once,        "Pret_Whistle"),
        new Entry("Stare_State",      "Pret_Stare_Audio",      PlayGhostAudioActionSO.Mode.LoopInState, "Pret_Stare_Loop", 0f, 1f),
        new Entry("Alert_State",      "Pret_Alert_Audio",      PlayGhostAudioActionSO.Mode.Once,        "Pret_Charge"),
        new Entry("Attack_State",     "Pret_Attack_Audio",     PlayGhostAudioActionSO.Mode.Once,        "Pret_Scream"),
        new Entry("BackHome_State",   "Pret_BackHome_Audio",   PlayGhostAudioActionSO.Mode.Once,        "Pret_BackHome"),
    };

    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Setup Pret Audio Table (ใส่เสียงเปรตใน table)")]
    private static void Run()
    {
        TransitionTableSO table = FindAsset<TransitionTableSO>(TableName);
        if (table == null)
        {
            EditorUtility.DisplayDialog("Pret Audio", $"หา {TableName} ไม่เจอ", "OK");
            return;
        }

        string tablePath = AssetDatabase.GetAssetPath(table);
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(tablePath);
        var report = new StringBuilder();
        var createdIds = new List<string>();

        foreach (Entry e in Entries)
        {
            StateSO state = FindSub<StateSO>(subAssets, e.State);
            if (state == null)
            {
                report.AppendLine($"✗ ไม่เจอ {e.State} — ข้าม");
                continue;
            }

            SoundID sound = FindOrCreateSoundId(e.Sound, out bool created);
            if (created) createdIds.Add(e.Sound);

            // action ชื่อเดิมมีแล้ว = ใช้ตัวเดิม (กดซ้ำไม่งอก)
            PlayGhostAudioActionSO action = FindSub<PlayGhostAudioActionSO>(subAssets, e.Action);
            if (action == null)
            {
                action = ScriptableObject.CreateInstance<PlayGhostAudioActionSO>();
                action.name = e.Action;
                AssetDatabase.AddObjectToAsset(action, table);
            }

            var so = new SerializedObject(action);
            so.FindProperty("_mode").enumValueIndex = (int)e.Mode;
            so.FindProperty("_sound").objectReferenceValue = sound;
            so.FindProperty("_delay").floatValue = e.Delay;
            so.FindProperty("_fade").floatValue = e.Fade;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);

            bool added = AddToState(state, action);
            report.AppendLine($"✓ {e.State} ← {e.Sound} ({e.Mode}){(added ? "" : " — มีอยู่แล้ว")}");
        }

        // ---------- เพลง ----------
        SoundID music = FindOrCreateSoundId(MusicName, MusicFolderHint, out bool musicCreated);
        if (musicCreated) createdIds.Add(MusicName);
        AddMusic(table, subAssets, "EnterStare_State", "Pret_Music_Start", MusicOverrideActionSO.Mode.Push, music, report);
        AddMusic(table, subAssets, "BackHome_State", "Pret_Music_Stop", MusicOverrideActionSO.Mode.Pop, null, report);

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(tablePath);

        // เช็คว่า SoundID ไหนยังไม่มีไฟล์เสียงใน Lib_Pret
        var missing = MissingFromLibrary();
        if (!InAnyLibrary(music)) missing.Add($"{MusicName} (Mixer = BGM)");
        if (createdIds.Count > 0)
            report.AppendLine($"\nสร้าง SoundID ใหม่: {string.Join(", ", createdIds)}");
        if (missing.Count > 0)
            report.AppendLine($"\nยังไม่มีใน SoundLibrary ไหนเลย (ไม่มีเสียง จนกว่าจะเพิ่ม):\n  {string.Join("\n  ", missing)}");

        Debug.Log($"[PretAudioTable]\n{report}", table);
        EditorUtility.DisplayDialog("Pret Audio", report.ToString(), "OK");
        Selection.activeObject = table;
    }

    // ---------- helpers ----------

    private static void AddMusic(TransitionTableSO table, Object[] subAssets, string stateName, string actionName,
                                 MusicOverrideActionSO.Mode mode, SoundID music, StringBuilder report)
    {
        StateSO state = FindSub<StateSO>(subAssets, stateName);
        if (state == null)
        {
            report.AppendLine($"✗ ไม่เจอ {stateName} — ข้ามเพลง");
            return;
        }

        MusicOverrideActionSO action = FindSub<MusicOverrideActionSO>(subAssets, actionName);
        if (action == null)
        {
            action = ScriptableObject.CreateInstance<MusicOverrideActionSO>();
            action.name = actionName;
            AssetDatabase.AddObjectToAsset(action, table);
        }

        var so = new SerializedObject(action);
        so.FindProperty("_mode").enumValueIndex = (int)mode;
        so.FindProperty("_music").objectReferenceValue = music;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(action);

        bool added = AddToState(state, action);
        string what = mode == MusicOverrideActionSO.Mode.Pop ? "คืนเพลงเดิม" : $"เปลี่ยนเพลง {MusicName}";
        report.AppendLine($"♪ {stateName} ← {what}{(added ? "" : " — มีอยู่แล้ว")}");
    }

    // อยู่ใน SoundLibrary ตัวไหนก็ได้ (Lib_Pret / Lib_PretBGM / Lib_BGM ...)
    private static bool InAnyLibrary(SoundID id)
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

    private static SoundID FindOrCreateSoundId(string name, out bool created) =>
        FindOrCreateSoundId(name, SoundIdFolderHint, out created);

    private static SoundID FindOrCreateSoundId(string name, string folderHint, out bool created)
    {
        created = false;
        SoundID found = FindAsset<SoundID>(name);
        if (found != null) return found;

        string folder = "Assets";
        SoundID hint = FindAsset<SoundID>(folderHint);
        if (hint != null) folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(hint)).Replace('\\', '/');

        var id = ScriptableObject.CreateInstance<SoundID>();
        AssetDatabase.CreateAsset(id, $"{folder}/{name}.asset");
        created = true;
        return id;
    }

    private static List<string> MissingFromLibrary()
    {
        var missing = new List<string>();
        foreach (Entry e in Entries)
        {
            SoundID id = FindAsset<SoundID>(e.Sound);
            if (!InAnyLibrary(id) && !missing.Contains(e.Sound)) missing.Add(e.Sound);
        }
        return missing;
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

    private static T FindSub<T>(Object[] assets, string exactName) where T : Object
    {
        foreach (Object o in assets)
            if (o is T typed && o.name == exactName) return typed;
        return null;
    }
}
#endif
