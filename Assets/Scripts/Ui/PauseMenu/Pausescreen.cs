using UnityEngine;
using Yantra.UI;

public class PauseScreen : UIScreen
{
    public void OnResumeClicked()
    {
        PauseController.Instance?.Resume();
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

    /// <summary>กด ESC ตอนอยู่หน้านี้ = Resume ไม่ใช่แค่ปิดหน้าจอเฉย ๆ</summary>
    public override bool HandleBack()
    {
        PauseController.Instance?.Resume();
        return true;   // จัดการเองแล้ว UIManager ไม่ต้องทำอะไรต่อ
    }
}