using System;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    // Connects the quest system to the save system (SaveManager). Put it in the same scene as the QuestManager.
    // Quest progress is stored as the "quests" section of the main save file; this script no longer writes a file.
    //
    // QuestManager restores itself in its own Start() by calling QuestManager.LoadProvider.
    // The order of Start() between two objects is not fixed, so the provider is set in Awake
    // (every Awake runs before any Start) instead of using the usual pull in Start.
    public class QuestSaveable : SaveableBehaviour<QuestSaveData>
    {
        protected override string DefaultKey => "quests";

        private void Awake()
        {
            QuestManager.LoadProvider = LoadFromSave;
        }

        private void OnDestroy()
        {
            if (QuestManager.LoadProvider == (Func<QuestSaveData>)LoadFromSave) QuestManager.LoadProvider = null;
        }

        private QuestSaveData LoadFromSave()
        {
            SaveManager manager = SaveManager.Instance;
            if (manager == null || !manager.TryGetSection(SaveKey, out SaveSection section)) return null;

            try
            {
                return JsonUtility.FromJson<QuestSaveData>(section.json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Quest] Could not read saved quests: {e.Message}", this);
                return null;
            }
        }

        // No QuestManager in this scene: return null so the quests saved earlier are kept.
        protected override QuestSaveData Capture() =>
            QuestManager.Instance != null ? QuestManager.Instance.Capture() : null;

        // QuestManager pulls the data itself through LoadProvider.
        protected override void Restore(QuestSaveData state, int version) { }

        protected override void Start() { }
    }
}
