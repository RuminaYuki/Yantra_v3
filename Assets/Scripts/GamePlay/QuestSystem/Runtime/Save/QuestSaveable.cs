using System;
using System.IO;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    // Saves / loads quest progress as JSON in Application.persistentDataPath.
    // Works on its own for now. When SaveManager is built, it can call ToJson() / FromJson()
    // instead and this file-writing part can be removed.
    // Put it in the same scene as the QuestManager.
    public class QuestSaveable : MonoBehaviour
    {
        [SerializeField] private string fileName = "quests.json";

        [Tooltip("Load the file when the scene starts. QuestManager reads it in its Start().")]
        [SerializeField] private bool loadOnStart = true;

        [Tooltip("Save after every completed quest (simple autosave).")]
        [SerializeField] private bool saveOnQuestCompleted;

        public string FilePath => Path.Combine(Application.persistentDataPath, fileName);

        private void Awake()
        {
            // Every Awake runs before any Start, so this is set before QuestManager.Start() reads it.
            if (loadOnStart) QuestManager.LoadProvider = LoadFromDisk;
            QuestManager.QuestCompleted += OnQuestCompleted;
        }

        private void OnDestroy()
        {
            if (QuestManager.LoadProvider == (Func<QuestSaveData>)LoadFromDisk) QuestManager.LoadProvider = null;
            QuestManager.QuestCompleted -= OnQuestCompleted;
        }

        private void OnQuestCompleted(QuestSO quest, int count)
        {
            if (saveOnQuestCompleted) Save();
        }

        // ------------------------------------------------------------------ JSON helpers (for SaveManager later)

        public static string ToJson(QuestSaveData data) => JsonUtility.ToJson(data);

        public static QuestSaveData FromJson(string json) =>
            string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<QuestSaveData>(json);

        // ------------------------------------------------------------------ File

        // Hook to a SavePoint / UnityEvent.
        public void Save()
        {
            var manager = QuestManager.Instance;
            if (manager == null) return;

            try
            {
                string path = FilePath;
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, ToJson(manager.Capture()));

                // Write to .tmp first so a crash never leaves a half-written save; keep the old one as .bak.
                if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
                else File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Quest] Save failed: {e.Message}", this);
            }
        }

        public QuestSaveData LoadFromDisk()
        {
            try
            {
                string path = FilePath;
                if (File.Exists(path)) return FromJson(File.ReadAllText(path));
                if (File.Exists(path + ".bak")) return FromJson(File.ReadAllText(path + ".bak"));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Quest] Load failed: {e.Message}", this);
            }
            return null;
        }

        // Loads the file into the running QuestManager now (e.g. a Continue button in the same scene).
        public void LoadNow()
        {
            var data = LoadFromDisk();
            if (data != null && QuestManager.Instance != null) QuestManager.Instance.Restore(data);
        }

        [ContextMenu("Delete Save File")]
        public void DeleteSave()
        {
            foreach (var p in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp" })
            {
                if (File.Exists(p)) File.Delete(p);
            }
        }

        [ContextMenu("Save Now")]
        private void SaveNowMenu() => Save();
    }
}
