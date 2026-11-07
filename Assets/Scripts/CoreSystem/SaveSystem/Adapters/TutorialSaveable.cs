using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Saves which tutorials the player has already seen.
// Put it on the player root (next to PlayerSaveable), NOT on the UI:
// UIRoot survives scene loads and TutorialScreen starts disabled, so Start / Restore would not run.
public class TutorialSaveable : SaveableBehaviour<TutorialSaveable.SaveData>
{
    [Serializable]
    public class SaveData { public List<string> seenTutorials = new List<string>(); }

    protected override string DefaultKey => "tutorials";

    protected override SaveData Capture() =>
        new SaveData { seenTutorials = new List<string>(TutorialScreen.SeenKeys) };

    protected override void Restore(SaveData state, int version) =>
        TutorialScreen.RestoreSeen(state.seenTutorials);

    // Clear the list on every level load; Restore (in Start) fills it again from the save.
    // Without this, a New Game in the same run would keep the old list.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        // Domain Reload is off, so unsubscribe first to avoid adding the handler twice.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive loads (UI, sub-scenes) are not a new level, keep the list.
        if (mode != LoadSceneMode.Single) return;
        TutorialScreen.ResetSeen();
    }
}
