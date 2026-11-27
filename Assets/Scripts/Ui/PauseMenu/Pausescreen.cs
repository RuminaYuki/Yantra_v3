using UnityEngine;
using Yantra.UI;

public class PauseScreen : UIScreen
{
    public void OnResumeClicked()
    {
        PauseController.Instance?.Resume();
    }

    /// <summary>เปิดหน้าเลือกเซฟทับ Pause — กด Back แล้วกลับมาหน้านี้</summary>
    public void OnLoadGameClicked()
    {
        UIManager.Instance.Open(ScreenId.LoadGame);
    }

    public void OnSettingsClicked()
    {
        UIManager.Instance.Open(ScreenId.Settings);
    }

    public void OnMainMenuClicked()
    {
        // ต้องคืนเวลาก่อนโหลด ไม่งั้น coroutine ใน SceneLoader ที่ใช้เวลาปกติจะค้าง
        PauseController.Instance?.ForceResumeTime();

        UIManager.Instance.CloseAll(instant: true);
        SceneLoader.Instance.LoadMainMenu();
    }

    // โหลดเซฟจากหน้า Load Game ที่เปิดทับ Pause อยู่ — คืนเวลากับเสียงก่อน เหมือนตอนกด Main Menu
    // (หน้า Pause ยังเปิดค้างใน stack ตอนหน้า Load Game ทับ เลยฟังจากตรงนี้ได้)
    protected override void OnOpening()
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadStarted += OnLoadStarted;
    }

    protected override void OnClosing()
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadStarted -= OnLoadStarted;
    }

    protected override void OnDestroy()
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadStarted -= OnLoadStarted;
        base.OnDestroy();
    }

    private void OnLoadStarted(string sceneName)
    {
        PauseController.Instance?.ForceResumeTime();
    }

    /// <summary>กด ESC ตอนอยู่หน้านี้ = Resume ไม่ใช่แค่ปิดหน้าจอเฉย ๆ</summary>
    public override bool HandleBack()
    {
        PauseController.Instance?.Resume();
        return true;   // จัดการเองแล้ว UIManager ไม่ต้องทำอะไรต่อ
    }
}