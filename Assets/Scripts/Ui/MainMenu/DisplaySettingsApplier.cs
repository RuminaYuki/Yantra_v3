using UnityEngine;

/// <summary>
/// ชั้นที่ 2 — ฟัง GameSettings.GraphicsChanged แล้วเอาค่าไปใช้กับจอจริง
/// วางบน UIRoot (ตัวเดียวกับ UIManager)
/// </summary>
public class DisplaySettingsApplier : MonoBehaviour
{
    [Header("Brightness")]
    [Tooltip("Image สีดำเต็มจอที่วางทับทุกอย่าง ใช้ทำให้จอมืดลง")]
    [SerializeField] private CanvasGroup _brightnessOverlay;

    [Tooltip("ความมืดสูงสุดตอน brightness = 0.5")]
    [SerializeField, Range(0f, 0.9f)] private float _maxDarkness = 0.7f;

    private void OnEnable()
    {
        GameSettings.GraphicsChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.GraphicsChanged -= Apply;
    }

    private void Apply()
    {
        ApplyResolutionAndFullscreen();
        ApplyVSync();
        ApplyBrightness();
    }

    /// <summary>
    /// ต้องตั้งความละเอียดกับ fullscreen พร้อมกันในคำสั่งเดียว
    /// ถ้าแยกเรียก จอจะกระพริบ 2 รอบ และบางเครื่องจะได้ขนาดผิด
    /// </summary>
    private void ApplyResolutionAndFullscreen()
    {
        int w = GameSettings.ResolutionWidth;
        int h = GameSettings.ResolutionHeight;
        bool fullscreen = GameSettings.Fullscreen;

        if (w <= 0 || h <= 0)
        {
            w = Screen.currentResolution.width;
            h = Screen.currentResolution.height;
        }

        // ไม่มีอะไรเปลี่ยน ไม่ต้องสั่ง — การสั่งซ้ำทำให้จอกระพริบเปล่า ๆ
        if (Screen.width == w && Screen.height == h && Screen.fullScreen == fullscreen)
            return;

        var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(w, h, mode);
    }

    private void ApplyVSync()
    {
        QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0;
    }

    /// <summary>
    /// ใช้ overlay ดำทับแทนการแก้ gamma ของระบบ
    /// เพราะการแก้ gamma จริงต้องยุ่งกับ HDRP Exposure ซึ่งจะไปทับงาน lighting ของทีม
    /// วิธีนี้ปลอดภัยกว่า และคนเล่นเห็นผลเหมือนกัน
    /// </summary>
    private void ApplyBrightness()
    {
        if (_brightnessOverlay == null) return;

        float brightness = GameSettings.Brightness;   // 0.5 = มืด, 1 = ปกติ, 2 = สว่าง

        if (brightness >= 1f)
        {
            _brightnessOverlay.alpha = 0f;
        }
        else
        {
            float t = Mathf.InverseLerp(1f, 0.5f, brightness);
            _brightnessOverlay.alpha = t * _maxDarkness;
        }
    }
}