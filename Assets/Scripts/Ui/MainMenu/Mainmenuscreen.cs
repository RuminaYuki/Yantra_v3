using UnityEngine;
using UnityEngine.Device;
using Yantra.UI;

public class MainMenuScreen : UIScreen
{
    // ผูกฟังก์ชันพวกนี้กับ OnClick ของปุ่มใน Inspector

    public void OnNewGameClicked()
    {
        SceneLoader.Instance.LoadScene(SceneNames.Gameplay);
    }

    public void OnSettingsClicked()
    {
        UIManager.Instance.Open(ScreenId.Settings);
    }

    public void OnCreditsClicked()
    {
        UIManager.Instance.Open(ScreenId.Credits);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}