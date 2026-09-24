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

    public bool IsPaused { get; private set; }

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
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _playerHealth = playerObj.GetComponent<Health>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // กันเกมค้างแข็งถ้า object นี้หายไปตอน pause อยู่
        if (IsPaused)
        {
            Time.timeScale = 1f;
            StateMachineController.IsPaused = false;
        }
    }

    /// <summary>ตอนนี้กด pause ได้ไหม — ที่เดียวที่ตัดสินใจ</summary>
    public bool CanPause()
    {
        if (IsPaused) return false;

        if (_blockDuringCutscene && _cameraController != null && _cameraController.IsCutsceneMode)
            return false;

        if (_blockWhenDead && _playerHealth != null && _playerHealth.IsDead)
            return false;

        // มีหน้าจออื่นเปิดอยู่ (Game Over, Loading) ไม่ต้อง pause ซ้อน
        var ui = UIManager.Instance;
        if (ui != null && ui.HasAnyOpen) return false;

        return true;
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (!CanPause()) return;

        IsPaused = true;
        Time.timeScale = 0f;

        // timeScale = 0 หยุด Update ไม่ได้ state machine ยังอ่านปุ่มอยู่
        // ต้องสั่งหยุดเอง ไม่งั้นคลิกปุ่มในเมนู pause แล้วตัวละครต่อยตาม
        StateMachineController.IsPaused = true;

        if (_cameraController != null) _cameraController.IsPaused = true;

        UIManager.Instance?.Open(ScreenId.Pause);
        PauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;
        StateMachineController.IsPaused = false;

        if (_cameraController != null) _cameraController.IsPaused = false;

        UIManager.Instance?.CloseAll(instant: false);
        PauseChanged?.Invoke(false);
    }

    /// <summary>คืนเวลาโดยไม่สนใจสถานะ — ใช้ก่อนโหลด scene</summary>
    public void ForceResumeTime()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        StateMachineController.IsPaused = false;
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