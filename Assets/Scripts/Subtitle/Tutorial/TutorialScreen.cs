using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
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

    [Tooltip("ที่ฉายวิดีโอ — ไม่ใส่ก็ได้ ถ้าไม่ใช้หน้าที่มีวิดีโอ")]
    [SerializeField] private RawImage _videoImage;
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;

    [Tooltip("ตัวเลขหน้า เช่น 1/3 — ไม่ใส่ก็ได้ ถ้ามีหน้าเดียวจะซ่อนเอง")]
    [SerializeField] private TMP_Text _pageText;

    [Header("Continue Button")]
    [Tooltip("ไม่ต้องผูก OnClick ใน Inspector — โค้ดผูกให้เอง ถ้าผูกซ้ำจะข้ามทีละ 2 หน้า")]
    [SerializeField] private Button _continueButton;

    [Tooltip("ให้คลิกเมาส์ที่ปุ่มได้ด้วย — ปิดไว้ = กด E อย่างเดียว เมาส์ล็อกไว้กลางจอเหมือนตอนเล่น")]
    [SerializeField] private bool _allowMouseClick = false;
    [SerializeField] private TMP_Text _continueLabel;

    [Tooltip("พิมพ์ {E} เพื่อใส่ไอคอนปุ่ม เช่น {E} Next — ต้องมีไอคอนชื่อนี้ใน Sprite Asset")]
    [SerializeField] private string _nextLabel = "Next [E]";

    [Tooltip("พิมพ์ {E} เพื่อใส่ไอคอนปุ่ม เช่น {E} Close")]
    [SerializeField] private string _closeLabel = "Close [E]";

    [Tooltip("โชว์ตัวเลขนับถอยหลังบนปุ่มตอนยังล็อก — ปิดแล้วปุ่มยังล็อกอยู่ แค่ไม่มีเลขให้เห็น (จางไว้แทน)")]
    [SerializeField] private bool _showCountdown = true;

    [Tooltip("{0} = ข้อความปุ่ม, {1} = วินาทีที่เหลือ")]
    [SerializeField] private string _countdownFormat = "{0} ({1})";

    [Header("Continue Label Fade")]
    [Tooltip("ความทึบของปุ่มตอนยังล็อก — จางไว้ ผู้เล่นจะรู้ว่ายังกดไม่ได้")]
    [SerializeField, Range(0f, 1f)] private float _lockedAlpha = 0.4f;

    [Tooltip("ปลดล็อกแล้วค่อย ๆ เฟดขึ้นกี่วินาที — 0 = โผล่ทันที")]
    [SerializeField, Min(0f)] private float _appearFadeDuration = 0.4f;

    [Header("Sounds (เว้นว่าง = ไม่มีเสียง)")]
    [Tooltip("ตอนหน้า tutorial เปิดขึ้นมา")]
    [SerializeField] private SoundID _openSound;

    [Tooltip("ตอนกด E ไปหน้าถัดไป")]
    [SerializeField] private SoundID _nextSound;

    [Tooltip("ตอนกด E ปิด tutorial")]
    [SerializeField] private SoundID _closeSound;

    [Header("Pulse When Ready")]
    [Tooltip("ปลดล็อกแล้วให้กระพริบเบา ๆ บอกว่ากดได้แล้ว")]
    [SerializeField] private bool _pulseWhenReady = true;
    [SerializeField, Min(0f)] private float _pulseSpeed = 4f;

    [Tooltip("แถบไฮไลต์หลังปุ่ม (แบบ RE4) — ใส่แล้วแถบนี้กระพริบแทน ไอคอนกับตัวหนังสือจะนิ่ง\nเว้นว่าง = ตัวหนังสือกระพริบเอง")]
    [SerializeField] private Graphic _highlight;
    [SerializeField, Range(0f, 1f)] private float _highlightMinAlpha = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _highlightMaxAlpha = 0.6f;

    [Tooltip("ใช้ตอนไม่มีแถบไฮไลต์ — ตัวหนังสือจางสุดแค่ไหนตอนกระพริบ")]
    [SerializeField, Range(0f, 1f)] private float _pulseMinAlpha = 0.35f;

    // จำไว้ทั้งเกม (รวมตอน Retry / โหลดฉากใหม่) — ปิดเกมเปิดใหม่ถึงจะเห็นอีก
    private static readonly HashSet<TutorialDataSO> _seen = new();

    private readonly Queue<TutorialDataSO> _queue = new();
    private TutorialDataSO _current;
    private int _pageIndex;
    private float _lockTimer;
    private float _unlockedAt;
    private int _openFrame;

    private bool _frozen;
    private bool _unfreezePending;
    private int _closeFrame;
    private float _savedTimeScale = 1f;
    private CursorLockMode _savedCursorLock;
    private bool _savedCursorVisible;
    private PlayerCameraController _camera;
    private RenderTexture _videoTexture;

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
        if (_continueButton != null && _allowMouseClick)
            _continueButton.onClick.AddListener(Continue);

        SetupVideoPlayer();

        base.Awake();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_videoTexture != null)
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
        }
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
        PlaySound(_openSound);
        LoadNext();
    }

    protected override void OnClosing()
    {
        _queue.Clear();
        _current = null;
        StopVideo();

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

        UpdateLabelAlpha();

        if (_lockTimer > 0f)
        {
            _lockTimer -= Time.unscaledDeltaTime;
            if (_lockTimer <= 0f)
                _unlockedAt = Time.unscaledTime;

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
            PlaySound(_nextSound);
            ShowPage(_pageIndex + 1);
            return;
        }

        if (_queue.Count > 0)
        {
            PlaySound(_nextSound);
            LoadNext();
            return;
        }

        PlaySound(_closeSound);
        UIManager.Instance?.Close(ScreenId.Tutorial);
    }

    // ผ่าน SoundManager แบบเดียวกับ UISoundBank — ได้ mixer + ปรับเสียงใน Settings ได้
    // AudioSource ไม่สนใจ timeScale เลยดังได้แม้เกมหยุดอยู่
    private static void PlaySound(SoundID id)
    {
        if (id == null || SoundManager.Instance == null)
            return;

        SoundManager.Instance.PlayEventSFX(id);
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

        bool playVideo = page.Video != null && _videoPlayer != null && _videoImage != null;

        if (_image != null)
        {
            _image.sprite = page.Image;
            _image.gameObject.SetActive(!playVideo && page.Image != null);
        }

        if (playVideo) PlayVideo(page.Video);
        else StopVideo();

        InputIconText.Apply(_titleText, page.Title);
        InputIconText.Apply(_bodyText, page.Body);

        if (_pageText != null)
        {
            int count = _current.Pages.Count;
            _pageText.gameObject.SetActive(count > 1);
            _pageText.text = $"{index + 1}/{count}";
        }

        _lockTimer = _current.LockSeconds;
        _unlockedAt = Time.unscaledTime;
        RefreshButton();
        UpdateLabelAlpha();
    }

    // ไอคอนในข้อความจาง/สว่างตามตัวหนังสือด้วย เพราะอยู่ใน TMP ตัวเดียวกัน
    private void UpdateLabelAlpha()
    {
        bool locked = _lockTimer > 0f;
        float sinceUnlock = Time.unscaledTime - _unlockedAt;

        // 0 = ยังล็อก → 1 = เฟดขึ้นครบแล้ว — ไม่โผล่พรวด
        float appear = 0f;
        if (!locked)
            appear = _appearFadeDuration > 0f
                ? Mathf.SmoothStep(0f, 1f, sinceUnlock / _appearFadeDuration)
                : 1f;

        // เริ่มจากสว่างสุดตอนปลดล็อก แล้วค่อยหรี่ลง
        float pulse = 1f;
        if (!locked && _pulseWhenReady)
            pulse = (Mathf.Cos(sinceUnlock * _pulseSpeed) + 1f) * 0.5f;

        bool hasHighlight = _highlight != null;

        if (_continueLabel != null)
        {
            float target = hasHighlight ? 1f : Mathf.Lerp(_pulseMinAlpha, 1f, pulse);
            _continueLabel.alpha = Mathf.Lerp(_lockedAlpha, target, appear);
        }

        if (hasHighlight)
        {
            float target = Mathf.Lerp(_highlightMinAlpha, _highlightMaxAlpha, pulse);
            _highlight.canvasRenderer.SetAlpha(target * appear);
        }
    }

    private void SetupVideoPlayer()
    {
        if (_videoPlayer == null)
            return;

        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = true;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        _videoPlayer.skipOnDrop = true;

        // ตอนเปิด tutorial เกมหยุดเวลาอยู่ — ใช้เวลาปกติวิดีโอจะค้างเฟรมแรก
        _videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
    }

    private void PlayVideo(VideoClip clip)
    {
        EnsureVideoTexture((int)clip.width, (int)clip.height);

        // ล้างภาพคลิปก่อนหน้าออก ไม่งั้นจะเห็นเฟรมเก่าแวบหนึ่งก่อนคลิปใหม่เริ่ม
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = _videoTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = previous;

        _videoImage.gameObject.SetActive(true);
        _videoPlayer.clip = clip;
        _videoPlayer.Play();
    }

    private void StopVideo()
    {
        if (_videoPlayer != null && _videoPlayer.isPlaying)
            _videoPlayer.Stop();

        if (_videoImage != null)
            _videoImage.gameObject.SetActive(false);
    }

    // สร้างใหม่เฉพาะตอนขนาดคลิปไม่ตรงกับของเดิม
    private void EnsureVideoTexture(int width, int height)
    {
        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);

        if (_videoTexture != null && _videoTexture.width == width && _videoTexture.height == height)
            return;

        if (_videoTexture != null)
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
        }

        _videoTexture = new RenderTexture(width, height, 0) { name = "TutorialVideo" };
        _videoTexture.Create();

        _videoPlayer.targetTexture = _videoTexture;
        _videoImage.texture = _videoTexture;
    }

    private void RefreshButton()
    {
        bool locked = _lockTimer > 0f;
        bool isLast = _current != null && _pageIndex >= _current.Pages.Count - 1 && _queue.Count == 0;
        string label = isLast ? _closeLabel : _nextLabel;

        // ปิดเมาส์ = ปุ่มรับคลิก/Enter ไม่ได้เลย ใช้ E อย่างเดียว
        if (_continueButton != null)
            _continueButton.interactable = _allowMouseClick && !locked;

        // Format ก่อนแล้วค่อยแปลงไอคอน — ไม่งั้น {0} {1} ของนับถอยหลังจะหาย
        string text = locked && _showCountdown
            ? string.Format(_countdownFormat, label, Mathf.CeilToInt(_lockTimer))
            : label;

        InputIconText.Apply(_continueLabel, text);
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

        // จำสถานะเมาส์ก่อนสั่งกล้องหยุด — กล้องตอนหยุดจะปลดเมาส์ให้ (ไว้ใช้กับเมนู Pause)
        // ถ้าจำหลังจากนั้น จะได้ค่า "เมาส์โผล่" ไปคืนตอนปิด เมาส์เลยค้างโผล่กลางเกม
        _savedCursorLock = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;

        _camera = FindAnyObjectByType<PlayerCameraController>();
        if (_camera != null) _camera.IsPaused = true;

        // ล็อกซ้ำ เพราะกล้องเพิ่งปลดเมาส์ไป
        Cursor.lockState = _allowMouseClick ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _allowMouseClick;
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

        // คืนเมาส์ก่อน แล้วค่อยปล่อยกล้อง — ให้กล้องได้ตัดสินเป็นคนสุดท้ายเหมือนตอน Resume จาก Pause
        Cursor.lockState = _savedCursorLock;
        Cursor.visible = _savedCursorVisible;

        if (_camera != null) _camera.IsPaused = false;
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