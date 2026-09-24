using UnityEngine;
using Yantra.UI;

// ค้างเกมแวบเดียวตอนตีโดน ให้หมัดรู้สึกหนัก
// เรียกได้จากทุกที่: HitStop.Freeze(0.05f) — ไม่ต้องวางในซีน สร้างตัวเองตอนใช้ครั้งแรก
public class HitStop : MonoBehaviour
{
    private static HitStop _instance;

    private float _freezeUntil;
    private bool _freezing;

    // ปิด Domain Reload ไว้ ค่า static ไม่ล้างเองตอนกด Play ใหม่
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    public static void Freeze(float duration)
    {
        if (duration <= 0f) return;

        // ต่อยรัวๆ ระหว่างค้าง = ยืดให้จบตามตัวที่ช้าสุด ไม่เอามาบวกกันจนค้างนาน
        if (_instance != null && _instance._freezing)
        {
            _instance._freezeUntil = Mathf.Max(_instance._freezeUntil, Time.unscaledTime + duration);
            return;
        }

        if (SomeoneElseOwnsTime()) return;

        if (_instance == null)
            _instance = new GameObject("[HitStop]").AddComponent<HitStop>();

        _instance._freezing = true;
        _instance._freezeUntil = Time.unscaledTime + duration;
        Time.timeScale = 0f;
    }

    // pause / game over / เมนูเปิดอยู่ = มีคนอื่นคุมเวลาอยู่ ห้ามไปยุ่ง
    private static bool SomeoneElseOwnsTime()
    {
        if (Time.timeScale <= 0f) return true;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return true;
        if (UIManager.Instance != null && UIManager.Instance.HasAnyOpen) return true;
        return false;
    }

    // Update ยังวิ่งตอน timeScale = 0 แค่ deltaTime เป็น 0 เลยต้องนับด้วยเวลาจริง
    private void Update()
    {
        if (!_freezing) return;
        if (Time.unscaledTime < _freezeUntil) return;

        Release();
    }

    private void Release()
    {
        _freezing = false;

        // ⚠️ ระหว่างค้าง ถ้าคนเล่นกด ESC หรือตายพอดี ระบบนั้นเป็นเจ้าของเวลาแล้ว
        // ถ้าเราคืนเป็น 1 ทับ เกมจะหลุด pause เอง
        if (Time.timeScale != 0f) return;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return;
        if (UIManager.Instance != null && UIManager.Instance.HasAnyOpen) return;

        Time.timeScale = 1f;
    }

    // ถูกลบกลางคัน (เปลี่ยนซีน) กันเกมค้างแข็ง
    private void OnDisable()
    {
        if (_freezing) Release();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}