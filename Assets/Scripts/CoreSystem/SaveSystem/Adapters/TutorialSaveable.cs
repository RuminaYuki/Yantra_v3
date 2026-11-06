using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary: clears the "seen tutorials" list every time a scene loads,
// so tutorials show again after Retry / loading a save.
// Works by itself; this component does not need to be placed in a scene.
// Later this can become a real saveable that stores the seen list in the save file.
public class TutorialSaveable : MonoBehaviour
{
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
