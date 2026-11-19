using System;
using UnityEngine;
using Yantra.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    [Header("References")]
    [Tooltip("เว้นว่างได้ จะหาให้เอง")]
    [SerializeField] private PlayerCameraController _cameraController;

    [Header("Blockers")]
    [Tooltip("ห้าม pause ตอนเล่น cutscene")]
    [SerializeField] private bool _blockDuringCutscene = true;

    [Tooltip("ห้าม pause ตอนตายแล้ว")]
    [SerializeField] private bool _blockWhenDead = true;

    [Tooltip("Health ของผู้เล่น — เว้นว่างได้ จะหาให้เอง")]
    [SerializeField] private Health _playerHealth;

    [Tooltip("หน้าที่กด ESC แล้วเปิด Pause ทับได้ — ปิด Pause แล้วกลับไปหน้านั้นต่อ\nหน้าอื่น (เช่น Game Over, Loading) ยังกด Pause ไม่ได้เหมือนเดิม")]
    [SerializeField] private ScreenId[] _canPauseOver = { ScreenId.Tutorial };

    public bool IsPaused { get; private set; }

    // Pause ทับหน้าอื่นอยู่ (เช่น tutorial) — หน้านั้นคุมเวลา/เมาส์ของมันเอง ปิด Pause แล้วต้องคืนให้มัน
    private bool _pausedOverScreen;
    private CursorLockMode _savedCursorLock;
    private bool _savedCursorVisible;

    /// <summary>ให้ระบบอื่นฟังได้ เช่น audio, ghost AI</summary>
    public event Action<bool> PauseChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (_cameraController == null)
            _cameraController = FindAnyObjectByType<PlayerCameraController>();
    }

    private void Start()
    {
        // หาใน Start ไม่ใช่ Awake เผื่อ Player ถูก spawn ทีหลัง
        if (_playerHealth == null)
            _playerHealth = PlayerLookup.Find<Health>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // กันเกมค้างแข็งถ้า object นี้หายไปตอน pause อยู่
        if (IsPaused)
        {
            Time.timeScale = 1f;
            StateMachineController.IsPaused = false;
            AudioListener.pause = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAudioPause()
    {
        // โปรเจกต์ปิด Domain Reload — กด Stop ตอน Pause ค้างไว้ รอบหน้าเสียงจะเงียบทั้งเกม
        AudioListener.pause = false;
    }

    /// <summary>ตอนนี้กด pause ได้ไหม — ที่เดียวที่ตัดสินใจ</summary>
    public bool CanPause()
    {
        if (IsPaused) return false;

        if (_blockDuringCutscene && _cameraController != null && _cameraController.IsCutsceneMode)
            return false;

        if (_blockWhenDead)
        {
            // Start หาไม่เจอ (ผู้เล่น spawn ทีหลัง) ลองหาใหม่ตอนกด ESC
            if (_playerHealth == null)
                _playerHealth = PlayerLookup.Find<Health>();

            if (_playerHealth != null && _playerHealth.IsDead)
                return false;
        }

        // มีหน้าจออื่นเปิดอยู่ (Game Over, Loading) ไม่ต้อง pause ซ้อน — ยกเว้นหน้าที่อนุญาตไว้ (Tutorial)
        var ui = UIManager.Instance;
        if (ui != null && ui.HasAnyOpen && !CanPauseOver(ui.Current)) return false;

        return true;
    }

    private bool CanPauseOver(ScreenId id)
    {
        if (_canPauseOver == null) return false;
        foreach (ScreenId allowed in _canPauseOver)
            if (allowed == id) return true;
        return false;
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (!CanPause()) return;

        var ui = UIManager.Instance;
        _pausedOverScreen = ui != null && ui.HasAnyOpen;
        _savedCursorLock = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;

        IsPaused = true;
        Time.timeScale = 0f;

        // timeScale = 0 หยุด Update ไม่ได้ state machine ยังอ่านปุ่มอยู่
        // ต้องสั่งหยุดเอง ไม่งั้นคลิกปุ่มในเมนู pause แล้วตัวละครต่อยตาม
        StateMachineController.IsPaused = true;

        if (_cameraController != null) _cameraController.IsPaused = true;

        // หน้าข้างล่าง (tutorial) ล็อกเมาส์ไว้ และกล้องหยุดอยู่ก่อนแล้วเลยไม่ปลดให้ — ปลดเองให้กดเมนูได้
        if (_pausedOverScreen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // AudioSource ไม่สนใจ timeScale — ต้องหยุดเสียงเอง
        // หยุดทุกเสียงในเกม ยกเว้นเพลงกับเสียงปุ่ม (ตั้ง ignoreListenerPause ไว้ใน SoundManager)
        AudioListener.pause = true;
        if (SoundManager.Instance != null) SoundManager.Instance.PauseVoice();

        UIManager.Instance?.Open(ScreenId.Pause);
        PauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;

        if (_pausedOverScreen)
        {
            _pausedOverScreen = false;
            var ui = UIManager.Instance;
            ClosePauseScreens(ui);

            // หน้าข้างล่างยังเปิดอยู่ — มันยังหยุดเกมอยู่ แค่คืนเมาส์กับเสียงให้เหมือนก่อนกด Pause
            if (ui != null && ui.HasAnyOpen)
            {
                Cursor.lockState = _savedCursorLock;
                Cursor.visible = _savedCursorVisible;
                AudioListener.pause = false;
                if (SoundManager.Instance != null) SoundManager.Instance.ResumeVoice();
                PauseChanged?.Invoke(false);
                return;
            }
        }

        Time.timeScale = 1f;
        StateMachineController.IsPaused = false;

        if (_cameraController != null) _cameraController.IsPaused = false;

        AudioListener.pause = false;
        if (SoundManager.Instance != null) SoundManager.Instance.ResumeVoice();

        UIManager.Instance?.CloseAll(instant: false);
        PauseChanged?.Invoke(false);
    }

    // ปิดเฉพาะ Pause กับหน้าที่ซ้อนอยู่บนมัน (เช่น Settings) — หน้าข้างล่างยังอยู่
    private static void ClosePauseScreens(UIManager ui)
    {
        if (ui == null) return;

        int guard = 0;
        while (ui.IsOpen(ScreenId.Pause) && ui.Current != ScreenId.None && guard++ < 16)
            ui.Close(ui.Current, instant: false);
    }

    /// <summary>คืนเวลาโดยไม่สนใจสถานะ — ใช้ก่อนโหลด scene</summary>
    public void ForceResumeTime()
    {
        _pausedOverScreen = false;
        IsPaused = false;
        Time.timeScale = 1f;
        StateMachineController.IsPaused = false;
        AudioListener.pause = false;
    }

    private void Update()
    {
        if (!PausePressed()) return;

        //Debug.Log($"ESC | CanPause={CanPause()} | IsPaused={IsPaused} | HasAnyOpen={UIManager.Instance?.HasAnyOpen}");

        // ถ้ามีหน้าจออื่นซ้อนอยู่บน Pause (เช่น Settings) ให้ย้อนกลับแทน
        var ui = UIManager.Instance;
        if (IsPaused && ui != null && ui.Current != ScreenId.Pause)
        {
            UISoundBank.Instance?.PlayBack();
            ui.Back();
            return;
        }

        // เปิด pause ใช้เสียงคลิก / ปิด pause ใช้เสียงย้อนกลับ
        if (IsPaused) UISoundBank.Instance?.PlayBack();
        else UISoundBank.Instance?.PlayClick(false);

        TogglePause();
    }

    private bool PausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}