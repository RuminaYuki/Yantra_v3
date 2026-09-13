using UnityEngine;
using UnityEngine.SceneManagement;
using Yantra.UI;

public class GameOverScreen : UIScreen
{
    [Header("Options")]
    [Tooltip("หยุดเวลาเกมตอนขึ้นหน้านี้")]
    [SerializeField] private bool _freezeTime = true;

    [Tooltip("หน่วงก่อนหน้าจอโผล่ ให้คนเล่นเห็นจังหวะตายก่อน")]
    [SerializeField] private float _delayBeforeShow = 1.2f;

    public float DelayBeforeShow => _delayBeforeShow;

    protected override void OnOpening()
    {
        if (_freezeTime) Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    protected override void OnClosing()
    {
        // คืนเวลาเสมอ ไม่งั้นถ้าปิดหน้านี้ด้วยวิธีอื่น เกมจะค้างแข็งถาวร
        if (_freezeTime) Time.timeScale = 1f;
    }

    /// <summary>เล่นด่านเดิมใหม่</summary>
    public void OnRetryClicked()
    {
        Time.timeScale = 1f;
        UIManager.Instance.CloseAll(instant: true);

        string currentScene = SceneManager.GetActiveScene().name;
        SceneLoader.Instance.LoadScene(currentScene);
    }

    public void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        UIManager.Instance.CloseAll(instant: true);

        // ต้องบอกด้วยว่าโหลดเสร็จแล้วให้เปิดหน้าไหน ไม่งั้นได้จอดำ
        SceneLoader.Instance.LoadScene(SceneNames.MainMenu, ScreenId.MainMenu);
    }

    /// <summary>กด ESC ตอนอยู่หน้านี้ไม่ควรปิดมันได้</summary>
    public override bool HandleBack() => true;
}