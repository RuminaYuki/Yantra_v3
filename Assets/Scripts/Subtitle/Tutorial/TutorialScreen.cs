using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yantra.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// หน้า tutorial — เปิดผ่าน TutorialScreen.TryShow(data) เท่านั้น
// เปิดแล้วหยุดเกมแบบเดียวกับ PauseController / ปิดแล้วคืนให้
// เป็น UIScreen ทำให้ HUD กับซับซ่อนเอง และกด ESC แล้วเมนู Pause ไม่ซ้อนขึ้นมา
public class TutorialScreen : UIScreen
{
    [Header("Content")]
    [SerializeField] private Image _image;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;

    [Tooltip("ตัวเลขหน้า เช่น 1/3 — ไม่ใส่ก็ได้ ถ้ามีหน้าเดียวจะซ่อนเอง")]
    [SerializeField] private TMP_Text _pageText;

    [Header("Continue Button")]
    [Tooltip("ไม่ต้องผูก OnClick ใน Inspector — โค้ดผูกให้เอง ถ้าผูกซ้ำจะข้ามทีละ 2 หน้า")]
    [SerializeField] private Button _continueButton;
    [SerializeField] private TMP_Text _continueLabel;

    [SerializeField] private string _nextLabel = "Next [E]";
    [SerializeField] private string _closeLabel = "Close [E]";

    [Tooltip("{0} = ข้อความปุ่ม, {1} = วินาทีที่เหลือ")]
    [SerializeField] private string _countdownFormat = "{0} ({1})";

    // จำไว้ทั้งเกม (รวมตอน Retry / โหลดฉากใหม่) — ปิดเกมเปิดใหม่ถึงจะเห็นอีก
    private static readonly HashSet<TutorialDataSO> _seen = new();

    private readonly Queue<TutorialDataSO> _queue = new();
    private TutorialDataSO _current;
    private int _pageIndex;
    private float _lockTimer;
    private int _openFrame;

    private bool _frozen;
    private bool _unfreezePending;
    private int _closeFrame;
    private float _savedTimeScale = 1f;
    private CursorLockMode _savedCursorLock;
    private bool _savedCursorVisible;
    private PlayerCameraController _camera;

    // ปิด Domain Reload ไว้ — ไม่ล้างเอง กด Play รอบสองจะไม่มี tutorial ขึ้นเลย
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _seen.Clear();

    public static bool HasSeen(TutorialDataSO data) => data != null && _seen.Contains(data);

    /// <summary>ล้างประวัติ — เผื่ออยากให้ New Game เห็น tutorial ใหม่หมด</summary>
    public static void ResetSeen() => _seen.Clear();

    /// <summary>โชว์ tutorial — คืน false ถ้าเคยเห็นแล้วหรือเปิดไม่ได้</summary>
    public static bool TryShow(TutorialDataSO data)
    {
        if (data == null || data.Pages.Count == 0 || _seen.Contains(data))
            return false;

        if (!UIManager.TryGet(ScreenId.Tutorial, out TutorialScreen screen) || UIManager.Instance == null)
        {
            Debug.LogWarning("[TutorialScreen] ไม่พบหน้า Tutorial ใน UIRoot — เช็คว่า ScreenId = Tutorial และ object เปิดอยู่", data);
            return false;
        }

        _seen.Add(data);
        screen.Enqueue(data);
        return true;
    }

    protected override void Awake()
    {
        // ผูกก่อน base.Awake เพราะ base จะปิด GameObject นี้
        if (_continueButton != null)
            _continueButton.onClick.AddListener(Continue);

        base.Awake();
    }

    public override bool HandleBack() => true;

    private void Enqueue(TutorialDataSO data)
    {
        _queue.Enqueue(data);

        if (!IsOpen)
            UIManager.Instance.Open(ScreenId.Tutorial);
        else
            RefreshButton();   // หน้าสุดท้ายเปลี่ยนจาก "ปิด" เป็น "ถัดไป"
    }

    protected override void OnOpening()
    {
        _openFrame = Time.frameCount;
        Freeze();
        LoadNext();
    }

    protected override void OnClosing()
    {
        _queue.Clear();
        _current = null;

        // คืนเกมเฟรมถัดไป — ปุ่มปิดคือ E ซึ่งเป็นปุ่ม Interact ด้วย
        // ถ้าคืนในเฟรมเดียวกัน ระบบ Interact จะอ่าน E ตัวเดียวกันแล้วไปกดของข้างหน้าต่อ
        _unfreezePending = true;
        _closeFrame = Time.frameCount;
    }

    private void OnDisable()
    {
        // ปิดแบบ instant แล้ว object หายทันที Update ไม่ได้วิ่งต่อ
        if (_unfreezePending)
            Unfreeze();
    }

    private void Update()
    {
        if (_unfreezePending)
        {
            if (Time.frameCount > _closeFrame)
                Unfreeze();
            return;
        }

        if (!IsOpen || _current == null)
            return;

        if (_lockTimer > 0f)
        {
            _lockTimer -= Time.unscaledDeltaTime;
            RefreshButton();
            return;
        }

        // กันเคสเปิดจากการกด E (เช่นเก็บของ) แล้วข้ามหน้าแรกทันที
        if (Time.frameCount > _openFrame && ContinuePressed())
            Continue();
    }

    private void Continue()
    {
        if (_current == null || _lockTimer > 0f)
            return;

        if (_pageIndex < _current.Pages.Count - 1)
        {
            ShowPage(_pageIndex + 1);
            return;
        }

        if (_queue.Count > 0)
        {
            LoadNext();
            return;
        }

        UIManager.Instance?.Close(ScreenId.Tutorial);
    }

    private void LoadNext()
    {
        _current = _queue.Count > 0 ? _queue.Dequeue() : null;

        if (_current == null)
        {
            UIManager.Instance?.Close(ScreenId.Tutorial);
            return;
        }

        ShowPage(0);
    }

    private void ShowPage(int index)
    {
        _pageIndex = index;
        TutorialPage page = _current.Pages[index];

        if (_image != null)
        {
            _image.sprite = page.Image;
            _image.gameObject.SetActive(page.Image != null);
        }

        if (_titleText != null) _titleText.text = page.Title;
        if (_bodyText != null) _bodyText.text = page.Body;

        if (_pageText != null)
        {
            int count = _current.Pages.Count;
            _pageText.gameObject.SetActive(count > 1);
            _pageText.text = $"{index + 1}/{count}";
        }

        _lockTimer = _current.LockSeconds;
        RefreshButton();
    }

    private void RefreshButton()
    {
        bool locked = _lockTimer > 0f;
        bool isLast = _current != null && _pageIndex >= _current.Pages.Count - 1 && _queue.Count == 0;
        string label = isLast ? _closeLabel : _nextLabel;

        if (_continueButton != null)
            _continueButton.interactable = !locked;

        if (_continueLabel != null)
            _continueLabel.text = locked
                ? string.Format(_countdownFormat, label, Mathf.CeilToInt(_lockTimer))
                : label;
    }

    private void Freeze()
    {
        // เปิดใหม่ระหว่างกำลังเฟดปิด — ยกเลิกการคืนเกมที่ค้างอยู่ ไม่งั้นเกมจะเดินทั้งที่หน้ายังเปิด
        _unfreezePending = false;

        if (_frozen)
            return;

        _frozen = true;

        // hitstop อาจตั้ง 0 ค้างไว้อยู่ — ถ้าจำ 0 ไว้คืน เกมจะแข็งถาวร
        _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        // timeScale = 0 หยุด State Machine ไม่ได้ ต้องสั่งเอง (แบบเดียวกับ PauseController)
        StateMachineController.IsPaused = true;

        _camera = FindAnyObjectByType<PlayerCameraController>();
        if (_camera != null) _camera.IsPaused = true;

        _savedCursorLock = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Unfreeze()
    {
        _unfreezePending = false;

        if (!_frozen)
            return;

        _frozen = false;

        // มีคนกด Pause ต่อพอดี ให้ PauseController เป็นเจ้าของเวลาไป
        if (PauseController.Instance != null && PauseController.Instance.IsPaused)
            return;

        Time.timeScale = _savedTimeScale;
        StateMachineController.IsPaused = false;

        if (_camera != null) _camera.IsPaused = false;

        Cursor.lockState = _savedCursorLock;
        Cursor.visible = _savedCursorVisible;
    }

    private static bool ContinuePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}