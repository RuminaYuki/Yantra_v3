using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// พิมพ์รหัสลับระหว่างเล่น → เปิด/ปิด _panel (ไม่ต้องมีช่องพิมพ์) — ใช้ได้ทั้งใน Editor และ build
// วางบน object ที่ active ตลอด (เช่น Canvas) ส่วน _panel เป็นลูกที่ถูกเปิดปิด
// ปุ่มใน panel ผูก OnClick กับอะไรก็ได้ตามต้องการ
public class DebugPanel : MonoBehaviour
{
    [Tooltip("CanvasGroup ของ panel ที่มีปุ่มทั้งหมด — ถูกซ่อนให้เองตอนเริ่ม (object ยัง active อยู่)")]
    [SerializeField] private CanvasGroup _panel;

    [Tooltip("พิมพ์ตัวอักษรนี้เรียงกันระหว่างเล่น เพื่อเปิด/ปิด (ไม่สนตัวพิมพ์เล็ก/ใหญ่)")]
    [SerializeField] private string _secretCode = "debug";

    [Tooltip("ไม่ติ๊ก = build ไม่มี panel (ใน Editor ยังใช้ได้) — เอาติ๊กออกก่อนส่งเกมเวอร์ชันสุดท้าย")]
    [SerializeField] private bool _enabledInBuild = true;

    [Tooltip("ตอนเปิด panel: ปลดเมาส์ หยุดหมุนกล้อง ล็อกการเดิน — ปิด panel แล้วคืนค่าเดิม")]
    [SerializeField] private bool _lockPlayer = true;

    private string _typed = "";
    private bool _open;

    private PlayerCameraController _camera;
    private bool _cameraWasPaused;
    private BaseLocomotion _locomotion;

    private PauseController _pause;
    private bool _pauseWasEnabled;
    private int _restorePauseFrame = -1;   // -1 = ไม่ต้องคืน

#if ENABLE_INPUT_SYSTEM
    private Keyboard _keyboard;
#endif

    public bool IsOpen => _open;

    private void Awake()
    {
        if (!Application.isEditor && !_enabledInBuild)
        {
            gameObject.SetActive(false);
            return;
        }

        if (_panel != null)
            ShowPanel(false);
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // คีย์บอร์ดเปลี่ยนตัวได้ (ถอด/เสียบใหม่) — ผูก event ใหม่ทุกครั้งที่เปลี่ยน
        if (Keyboard.current != _keyboard)
        {
            if (_keyboard != null)
                _keyboard.onTextInput -= OnTextInput;
            _keyboard = Keyboard.current;
            if (_keyboard != null)
                _keyboard.onTextInput += OnTextInput;
        }
#else
        foreach (char c in Input.inputString)
            OnTextInput(c);
#endif

        // panel เปิดอยู่ + กด ESC = ปิด panel
        if (_open && EscapePressed())
            Close();

        // คืน PauseController หลังปิด panel ไป 1 เฟรม — ESC ของเฟรมที่ปิดจะได้ไม่ไปเปิด Pause ต่อ
        if (_restorePauseFrame >= 0 && Time.frameCount > _restorePauseFrame)
            RestorePause();
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (_keyboard != null)
            _keyboard.onTextInput -= OnTextInput;
        _keyboard = null;
#endif
        SetOpen(false);
        RestorePause();   // ถูกปิดกลางทาง คืนทันที ไม่รอเฟรมถัดไป
    }

    // จำตัวอักษรล่าสุดเท่าความยาวรหัส ถ้าตรงกัน = เปิด/ปิด
    private void OnTextInput(char c)
    {
        if (char.IsControl(c) || string.IsNullOrEmpty(_secretCode))
            return;

        _typed += char.ToLowerInvariant(c);
        if (_typed.Length > _secretCode.Length)
            _typed = _typed.Substring(_typed.Length - _secretCode.Length);

        if (_typed == _secretCode.ToLowerInvariant())
        {
            _typed = "";
            SetOpen(!_open);
        }
    }

    // ผูกกับปุ่มใน panel ได้ (OnClick → DebugPanel.Open / Close / Toggle)
    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);
    public void Toggle() => SetOpen(!_open);

    private void SetOpen(bool open)
    {
        if (_open == open)
            return;
        _open = open;

        if (_panel != null)
            ShowPanel(open);

        if (open)
            BlockPause();
        else
            _restorePauseFrame = Time.frameCount;

        if (_lockPlayer)
        {
            if (open)
                LockPlayer();
            else
                UnlockPlayer();
        }
    }

    // ซ่อนด้วย CanvasGroup — object ยัง active อยู่ สคริปต์บน panel ยังทำงานได้
    private void ShowPanel(bool show)
    {
        _panel.alpha = show ? 1f : 0f;
        _panel.interactable = show;
        _panel.blocksRaycasts = show;
    }

    // ปิด PauseController ไว้ระหว่าง panel เปิด — ESC จะได้ไม่เปิดเมนู Pause
    private void BlockPause()
    {
        _restorePauseFrame = -1;   // เปิดซ้ำก่อนคืนทัน — ยังบล็อกอยู่ ไม่ต้องจำค่าใหม่
        if (_pause != null)
            return;

        _pause = PauseController.Instance;
        if (_pause == null)
            return;

        _pauseWasEnabled = _pause.enabled;
        _pause.enabled = false;
    }

    private void RestorePause()
    {
        _restorePauseFrame = -1;
        if (_pause != null)
            _pause.enabled = _pauseWasEnabled;   // ถ้าระบบอื่นปิดไว้ก่อนแล้ว ก็คงปิดไว้เหมือนเดิม
        _pause = null;
    }

    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void LockPlayer()
    {
        _camera = FindFirstObjectByType<PlayerCameraController>();
        if (_camera != null)
        {
            _cameraWasPaused = _camera.IsPaused;
            _camera.IsPaused = true;   // ปลดเมาส์ + หยุดหมุนกล้อง
        }

        _locomotion = PlayerLookup.Find<PlayerLocomotion>();
        if (_locomotion != null)
            _locomotion.LockLocomotion(this);
    }

    private void UnlockPlayer()
    {
        if (_camera != null)
            _camera.IsPaused = _cameraWasPaused;
        if (_locomotion != null)
            _locomotion.UnlockLocomotion(this);

        _camera = null;
        _locomotion = null;
    }
}
