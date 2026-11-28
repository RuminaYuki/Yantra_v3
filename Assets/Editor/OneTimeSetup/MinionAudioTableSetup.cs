#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

/// <summary>
/// ใส่เสียงตามท่าลงใน table ลูกน้อง (BaseEnemyMinions_TransitionTable) — กดซ้ำได้ ไม่ใส่ซ้ำ
/// ใช้แบบ "ช่องเสียง" (Slot): table กลางสั่งแค่ว่าเป็นเสียงช่องไหน
/// ผีแต่ละตัว (ผีกะ / กระสือ / ผีทหาร) เลือกไฟล์เองใน GhostAudio > เสียงตามท่า
/// ผีที่ยังไม่มี GhostAudio หรือไม่ได้ใส่ช่องนั้น = เงียบ ไม่ error
///
///   Alert_State         → Alert      (ครั้งเดียว)  เห็นผู้เล่น
///   ExecuteAttack_State → Attack     (ครั้งเดียว)  ตี
///   Stager_State        → Hurt       (ครั้งเดียว)  โดนตี
///   PrapareGlory_State  → HurtHeavy  (ครั้งเดียว)  ล้มลงรอ Glory
///   ReadyGlory_State    → GloryIdle  (วนใน state) ระหว่างรอ Glory
///   Dead_State          → Death      (ครั้งเดียว)  ตาย (ไม่ใช่ตายจาก Glory)
/// </summary>
public static class MinionAudioTableSetup
{
    private const string TableName = "BaseEnemyMinions_TransitionTable";
    private const string Title = "Minion Audio";

    private struct Entry
    {
        public string State, Action;
        public GhostSoundSlot Slot;
        public PlayGhostAudioActionSO.Mode Mode;
        public float Fade;

        public Entry(string state, string action, GhostSoundSlot slot, PlayGhostAudioActionSO.Mode mode, float fade = 0.5f)
        {
            State = state; Action = action; Slot = slot; Mode = mode; Fade = fade;
        }
    }

    private static readonly Entry[] Entries =
    {
        new Entry("Alert_State",         "Minion_Alert_Audio",     GhostSoundSlot.Alert,     PlayGhostAudioActionSO.Mode.Once),
        new Entry("ExecuteAttack_State", "Minion_Attack_Audio",    GhostSoundSlot.Attack,    PlayGhostAudioActionSO.Mode.Once),
        new Entry("Stager_State",        "Minion_Hurt_Audio",      GhostSoundSlot.Hurt,      PlayGhostAudioActionSO.Mode.Once),
        new Entry("PrapareGlory_State",  "Minion_HurtHeavy_Audio", GhostSoundSlot.HurtHeavy, PlayGhostAudioActionSO.Mode.Once),
        new Entry("ReadyGlory_State",    "Minion_GloryIdle_Audio", GhostSoundSlot.GloryIdle, PlayGhostAudioActionSO.Mode.LoopInState, 0.5f),
        new Entry("Dead_State",          "Minion_Death_Audio",     GhostSoundSlot.Death,     PlayGhostAudioActionSO.Mode.Once),
    };

    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Setup Minion Audio Table (ใส่เสียงใน table ลูกน้อง)")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(Title, "ออกจาก Play Mode ก่อนนะ", "ตกลง");
            return;
        }

        TransitionTableSO table = FindAsset<TransitionTableSO>(TableName);
        if (table == null)
        {
            EditorUtility.DisplayDialog(Title, $"หา {TableName} ไม่เจอ", "ตกลง");
            return;
        }

        string tablePath = AssetDatabase.GetAssetPath(table);
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(tablePath);
        var report = new StringBuilder();

        foreach (Entry e in Entries)
        {
            StateSO state = FindSub<StateSO>(subAssets, e.State);
            if (state == null)
            {
                report.AppendLine($"✗ ไม่เจอ {e.State} — ข้าม");
                continue;
            }

            // action ชื่อเดิมมีแล้ว = ใช้ตัวเดิม (กดซ้ำไม่งอก)
            PlayGhostAudioActionSO action = FindSub<PlayGhostAudioActionSO>(subAssets, e.Action);
            if (action == null)
            {
                action = ScriptableObject.CreateInstance<PlayGhostAudioActionSO>();
                action.name = e.Action;
                AssetDatabase.AddObjectToAsset(action, table);
            }

            var so = new SerializedObject(action);
            so.FindProperty("_mode").intValue = (int)e.Mode;
            so.FindProperty("_slot").intValue = (int)e.Slot;
            so.FindProperty("_sound").objectReferenceValue = null;
            so.FindProperty("_delay").floatValue = 0f;
            so.FindProperty("_fade").floatValue = e.Fade;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);

            bool added = AddToState(state, action);
            report.AppendLine($"✓ {e.State} ← ช่อง {e.Slot}{(added ? "" : " (มีอยู่แล้ว)")}");
        }

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(tablePath);

        report.AppendLine("\nไฟล์เสียงของแต่ละผี ตั้งที่ GhostAudio > เสียงตามท่า บน prefab ของผีตัวนั้น");
        Debug.Log($"[MinionAudioTable]\n{report}", table);
        EditorUtility.DisplayDialog(Title, report.ToString(), "ตกลง");
        Selection.activeObject = table;
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
