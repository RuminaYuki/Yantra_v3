using UnityEngine;
using Yantra.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UIBackInputHandler : MonoBehaviour
{
    [Tooltip("หน้าที่กด Back แล้วไม่ควรปิด เช่น Main Menu ที่เป็นหน้าราก")]
    [SerializeField]
    private ScreenId[] _ignoredScreens =
    {
        ScreenId.MainMenu,
        ScreenId.Loading,
        ScreenId.GameOver,
    };

    [Tooltip("เพิ่มคลิกขวาเป็นปุ่มย้อนกลับด้วย — ระวังกดพลาดตอนลาก slider")]
    [SerializeField] private bool _allowRightClick = false;

    private void Update()
    {
        // scene เกมมี PauseController คุม ESC อยู่แล้ว ปล่อยให้มันจัดการทั้งหมด
        if (PauseController.Instance != null) return;

        var ui = UIManager.Instance;
        if (ui == null || !ui.HasAnyOpen) return;

        if (!BackPressed()) return;

        // หน้าราก กด Back แล้วไม่ต้องทำอะไร
        foreach (var id in _ignoredScreens)
            if (ui.Current == id) return;

        UISoundBank.Instance?.PlayBack();
        ui.Back();
    }

    private bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) return true;

        if (_allowRightClick && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            return true;

        return false;
#else
        if (Input.GetKeyDown(KeyCode.Escape)) return true;
        if (_allowRightClick && Input.GetMouseButtonDown(1)) return true;
        return false;
#endif
    }
}