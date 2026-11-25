using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Yantra.UI;

// แม่แบบป้ายบังคับกด (แบบป้าย "กด F ค้าง" ของ Block) — ขึ้นหลังปิดหน้าสอน แล้วล็อกทุกอย่างจนผู้เล่นทำท่านั้นสำเร็จ
// ตัวนี้ทำส่วนที่ใช้ร่วมกัน: เริ่มจาก channel / รอ flag / ล็อก flag เดิน กล้อง / ป้ายใน UIRoot (ข้อความ รูปปุ่ม พื้นหลัง เต้นตุบๆ)
// ลูกทำแค่ขั้นตอนของตัวเอง: OnPromptStart → ShowStep(ขั้น) → ... → Complete()
//
// ป้ายใช้ SpellPrompt ใน UIRoot (SpellCastPromptView) ร่วมกัน — ขึ้นทีละอันอยู่แล้ว
// ไม่มีใน UIRoot = สร้างป้ายตัวหนังสือเอง
public abstract class ForcedActionPrompt : MonoBehaviour
{
    private enum State { Idle, WaitFlag, Running, Done, FadeOut }

    [Header("เริ่มเมื่อ")]
    [Tooltip("ช่องเดียวกับ On Closed ของหน้าสอน — ปิดหน้าสอนแล้วป้ายขึ้นทันที")]
    [SerializeField] private VoidEventChannelSO _startChannel;

    [Header("ล็อกระหว่างบังคับ")]
    [Tooltip("flag ที่ต้องเปิดก่อนถึงจะทำท่านั้นได้ (เช่น PlayerEnableSpell) — ยังปิดอยู่ = รอ / ว่าง = ไม่ต้องรอ")]
    [FormerlySerializedAs("_spellFlag")]
    [SerializeField] private FlagSO _requiredFlag;

    [Tooltip("ปิดหน้าสอนแล้ว flag ยังไม่เปิด (เช่น ท่าเก็บของยังไม่จบ) — รอได้นานสุดกี่วินาที (เวลาในเกม) เกินแล้วยกเลิก กันผู้เล่นค้าง")]
    [FormerlySerializedAs("_waitForSpellFlag")]
    [SerializeField, Min(0f)] private float _waitForRequiredFlag = 5f;

    [Tooltip("flag ที่ปิดชั่วคราว (ต่อย / กัน / ปืน) — จบแล้วคืนค่าเดิม")]
    [SerializeField] private FlagSO[] _lockFlags;

    [SerializeField] private bool _lockMovement = true;

    [Tooltip("ล็อกการหันกล้อง ตั้งแต่ป้ายขึ้นจนทำสำเร็จ")]
    [SerializeField] private bool _lockCamera = true;

    [Tooltip("กล้องค่อยๆ หันกลับระดับสายตาก่อนล็อก ใช้เวลาประมาณกี่วินาที — มากขึ้น = นุ่มขึ้น / 0 = ล็อกทันที (กล้องวิ่งกลับแบบความเร็วคงที่)")]
    [SerializeField, Min(0f)] private float _cameraEaseTime = 0.35f;

    [Tooltip("ช่วงนุ่มนานกี่เท่าของ Camera Ease Time ก่อนล็อกจริง — 2.5 = เข้าใกล้ระดับสายตาเกือบสุดแล้วค่อยล็อก")]
    [SerializeField, Min(1f)] private float _cameraEaseHold = 2.5f;

    [Header("ทำสำเร็จ")]
    [Tooltip("ว่าง = ไม่โชว์ จบแล้วหายเลย")]
    [SerializeField] private string _doneText = "";
    [SerializeField, Min(0f)] private float _doneTime = 1f;

    [Header("พื้นหลังรูปปุ่ม (KeyBackground ใน SpellPrompt) — สี / ความเข้ม ปรับที่ Color ของ Image")]
    [Tooltip("ขนาดพื้นหลัง เทียบกับขนาดรูปปุ่ม — X = ความกว้าง, Y = ความสูง (1 = เท่ารูป)")]
    [SerializeField] private Vector2 _backgroundScale = new Vector2(1.3f, 1.6f);

    [Header("หน้าตา (ใช้ตอนไม่มี SpellPrompt ใน UIRoot — สร้างป้ายตัวหนังสือเอง)")]
    [Tooltip("ว่าง = ฟอนต์ตั้งต้นของ TMP / ป้าย Block ใช้ RSU Text_Regular SDF")]
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField, Min(8f)] private float _fontSize = 28f;
    [SerializeField] private bool _bold = true;
    [Tooltip("ตัวพิมพ์ใหญ่หมดแบบป้าย Block (HOLD F TO BLOCK)")]
    [SerializeField] private bool _upperCase = true;

    [Header("ขอบ + เงา ตัวหนังสือ (ค่าเดียวกับป้าย Block)")]
    [Tooltip("ขอบตัวหนังสือ — 0 = ไม่มี / 0.1-0.2 กำลังดี")]
    [SerializeField, Range(0f, 0.5f)] private float _textOutline = 0.15f;
    [SerializeField] private Color _textOutlineColor = Color.black;
    [SerializeField] private bool _textShadow = true;
    [SerializeField] private Color _textShadowColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField, Range(0f, 1f)] private float _textShadowSize = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _textShadowSoftness = 0.6f;
    [Tooltip("ตำแหน่งบนจอ (0-1) — 0.5, 0.28 = กลางจอค่อนล่าง เหนือซับ")]
    [SerializeField] private Vector2 _screenPosition = new Vector2(0.5f, 0.28f);
    [SerializeField, Min(0f)] private float _pulse = 0.06f;
    [SerializeField, Min(0f)] private float _pulseSpeed = 6f;

    [Tooltip("ป้ายค่อยๆ เฟดเข้า (ทุกครั้งที่เปลี่ยนขั้น) / เฟดออก (ตอนทำสำเร็จ) ใช้เวลากี่วินาที")]
    [SerializeField, Min(0.01f)] private float _fadeTime = 0.4f;

    [SerializeField] private string _playerTag = "Player";

    private const int SortingOrder = 450;   // ต่ำกว่าแถบดำคัตซีน / เมนู

    private State _state;
    private int _step = -1;
    private float _doneTimer;
    private float _waitTimer;

    private StateFlagsAccess _flags;
    private BaseLocomotion _locomotion;
    private PlayerCameraController _camera;
    private GameObject _cameraEaseTarget;
    private float _cameraEaseLeft;
    private readonly Dictionary<FlagSO, bool> _savedFlags = new();
    private bool _locked;

    private GameObject _root;           // ป้ายที่สร้างเอง (ไม่มี SpellPrompt ใน UIRoot)
    private SpellCastPromptView _view;  // ป้ายใน UIRoot
    private bool _viewStyled;
    private CanvasGroup _group;
    private TMP_Text _label;
    private UnityEngine.UI.Image _key;
    private bool _stepBackground;
    private float _keyScale = 1f;
    private float _fade;          // 0 = จาง → 1 = ชัด (เฟดเข้า / ออก)
    private float _menuVisible;   // 0 = มีเมนูเปิดทับ → 1 = โชว์
    private Vector2 _keyBaseSize = new Vector2(80f, 80f);
    private string _shownText;

    public bool IsRunning => _state != State.Idle;

    // ===== ให้ลูกเขียน =====

    /// <summary>ป้ายเริ่มแล้ว (ล็อกเรียบร้อย) — เรียก ShowStep(ขั้นแรก)</summary>
    protected abstract void OnPromptStart();

    /// <summary>ข้อความ / รูปปุ่ม / มีพื้นหลังมั้ย ของขั้นนี้</summary>
    protected abstract void GetStepVisual(int step, out string text, out Sprite icon, out bool background);

    /// <summary>ทุกเฟรมระหว่างบังคับ — เช็คว่าทำสำเร็จแล้วเรียก ShowStep / Complete</summary>
    protected virtual void OnPromptUpdate() { }

    /// <summary>หาของเพิ่มบนตัวผู้เล่น — คืน false = หาไม่เจอ ไม่บังคับ</summary>
    protected virtual bool FindPlayerParts(Transform playerRoot) => true;

    protected virtual void OnLocked() { }
    protected virtual void OnUnlocked() { }

    /// <summary>ตอนปลดล็อก ให้คืนกล้องมั้ย — false = มีระบบอื่นล็อกกล้องต่อแล้ว (เช่น กระเป๋าเปิดอยู่) ไม่ไปปลดทับ</summary>
    protected virtual bool ShouldReleaseCamera() => true;

    // ===== ให้ลูกเรียก =====

    protected int CurrentStep => _state == State.Running ? _step : -1;
    protected bool IsStep(int step) => _state == State.Running && _step == step;

    protected virtual void OnEnable()
    {
        if (_startChannel != null) _startChannel.Raised += Begin;
    }

    // ปิด Domain Reload ไว้ — ไม่ถอดออก Play รอบหน้า channel จะยังเรียกตัวเก่า
    // ถูกปิดกลางคัน (เปลี่ยนซีน / ตาย) — ปลดล็อกคืนทุกอย่าง ไม่งั้นผู้เล่นต่อยไม่ได้ถาวร
    protected virtual void OnDisable()
    {
        if (_startChannel != null) _startChannel.Raised -= Begin;

        Unlock();
        _state = State.Idle;
        _step = -1;
        DestroyUI();
    }

    [ContextMenu("Begin (ทดสอบ)")]
    public void Begin()
    {
        if (_state != State.Idle)
            return;

        if (!FindPlayer())
        {
            Debug.LogWarning($"[{GetType().Name}] {name}: หาผู้เล่น (StateFlags / ของที่ต้องใช้) ไม่เจอ — ไม่บังคับ", this);
            return;
        }

        // flag มักเปิดตอนท่าเก็บของจบ — ช้ากว่าตอนปิดหน้าสอน รอก่อน ยังไม่ล็อก / ไม่ขึ้นป้าย
        if (_requiredFlag != null && !_flags.Get(_requiredFlag))
        {
            _state = State.WaitFlag;
            _waitTimer = _waitForRequiredFlag;
            return;
        }

        StartPrompt();
    }

    private void StartPrompt()
    {
        _state = State.Running;
        Lock();
        OnPromptStart();
    }

    // เวลาในเกม — หยุดนับตอนหน้าสอน / Pause เปิด (ท่าเก็บของก็หยุดตามด้วย)
    private void UpdateWaitFlag()
    {
        if (_flags != null && _flags.Get(_requiredFlag))
        {
            StartPrompt();
            return;
        }

        _waitTimer -= Time.deltaTime;
        if (_waitTimer <= 0f)
        {
            _state = State.Idle;
            Debug.LogWarning($"[{GetType().Name}] {name}: รอ {_requiredFlag.name} เปิดเกิน {_waitForRequiredFlag:0.#} วิ — ไม่บังคับ (กันผู้เล่นค้าง)", this);
        }
    }

    private void Update()
    {
        switch (_state)
        {
            case State.Idle:
                return;

            case State.WaitFlag:
                UpdateWaitFlag();
                return;

            case State.Done:
                _doneTimer -= Time.unscaledDeltaTime;
                if (_doneTimer <= 0f)
                    _state = State.FadeOut;
                break;

            // ทำสำเร็จแล้ว ป้ายค่อยๆ จางหาย แล้วค่อยเก็บ
            case State.FadeOut:
                UpdateUI();
                if (_fade <= 0f)
                {
                    _state = State.Idle;
                    _step = -1;
                    DestroyUI();
                }
                return;

            case State.Running:
                OnPromptUpdate();
                if (_state != State.Running)
                    return;
                UpdateCameraLock();
                break;
        }

        UpdateUI();
    }

    /// <summary>เปลี่ยนป้ายเป็นขั้นนี้ (ข้อความ / รูปปุ่ม / พื้นหลัง จาก GetStepVisual)</summary>
    protected void ShowStep(int step)
    {
        if (_state != State.Running)
            return;

        _step = step;
        GetStepVisual(step, out string text, out Sprite icon, out bool background);
        ApplyVisual(text, icon, background);
    }

    /// <summary>ทำสำเร็จ — ปลดล็อก โชว์ Done Text (ถ้ามี) แล้วป้ายหาย</summary>
    protected void Complete()
    {
        if (_state != State.Running)
            return;

        Unlock();

        if (!string.IsNullOrEmpty(_doneText) && _doneTime > 0f)
        {
            _state = State.Done;
            _doneTimer = _doneTime;
            ApplyVisual(_doneText, null, false);
            return;
        }

        _state = State.FadeOut;   // จางหายก่อน Update ค่อยเก็บ
    }

    private void ApplyVisual(string text, Sprite icon, bool background)
    {
        EnsureUI();
        if (_shownText != text)
        {
            _shownText = text;
            InputIconText.Apply(_label, text ?? "");
            _fade = 0f;   // เนื้อหาใหม่ — ค่อยๆ เฟดขึ้นมาใหม่
        }

        _stepBackground = background;

        // รูปปุ่มของขั้นนี้ — ว่าง = ซ่อนรูป
        if (_key != null)
        {
            _key.enabled = icon != null;
            if (icon != null)
            {
                _key.sprite = icon;

                // รูปกว้าง (เช่น กดค้าง → ปล่อย) — ความสูงเท่าเดิม ขยายกว้างตามสัดส่วนรูป ไม่ให้โดนบีบ
                float aspect = icon.rect.height > 0f ? icon.rect.width / icon.rect.height : 1f;
                _key.rectTransform.sizeDelta = new Vector2(_keyBaseSize.y * aspect, _keyBaseSize.y);
            }
        }

        UpdateBackground();
    }

    // ===== ล็อก / ปลดล็อก =====

    private void Lock()
    {
        if (_locked)
            return;
        _locked = true;

        _savedFlags.Clear();
        if (_lockFlags != null)
        {
            foreach (FlagSO flag in _lockFlags)
            {
                if (flag == null || _savedFlags.ContainsKey(flag))
                    continue;
                _savedFlags[flag] = _flags.Get(flag);
                _flags.Set(flag, false);
            }
        }

        if (_lockMovement && _locomotion != null)
            _locomotion.LockLocomotion(this);

        if (_lockCamera && _camera != null && !StartCameraEase())
            _camera.IsLookLocked = true;

        OnLocked();
    }

    private void Unlock()
    {
        if (!_locked)
            return;
        _locked = false;

        // คืนเฉพาะ flag ที่เดิมเปิดอยู่ — ระหว่างนี้ระบบอื่นเปิดให้แล้ว (เช่น เก็บปืน) ก็ไม่ไปปิดทับ
        if (_flags != null)
        {
            foreach (KeyValuePair<FlagSO, bool> saved in _savedFlags)
            {
                if (saved.Value)
                    _flags.Set(saved.Key, true);
            }
        }
        _savedFlags.Clear();

        if (_locomotion != null)
            _locomotion.UnlockLocomotion(this);

        EndCameraEase();
        if (_lockCamera && _camera != null && ShouldReleaseCamera())
            _camera.IsLookLocked = false;

        OnUnlocked();
    }

    private void UpdateCameraLock()
    {
        // ช่วงกล้องหันนุ่มๆ กลับระดับสายตา — ครบเวลาแล้วค่อยล็อกจริง
        if (_cameraEaseLeft > 0f)
        {
            _cameraEaseLeft -= Time.deltaTime;
            if (_cameraEaseLeft <= 0f)
            {
                EndCameraEase();
                if (_camera != null)
                    _camera.IsLookLocked = true;
            }
        }
        // state อื่น (เช่น เวท) ปลดล็อกกล้องตอนออก — ล็อกซ้ำทุกเฟรมจนทำสำเร็จ
        else if (_locked && _lockCamera && _camera != null && !_camera.IsLookLocked)
        {
            _camera.IsLookLocked = true;
        }
    }

    // หันกล้องนุ่มๆ ไปมุมระดับสายตา (ทิศเดิม) ด้วย Forced Look ของกล้อง (SmoothDamp) — เมาส์หันไม่ได้ระหว่างนี้
    // ถ้าล็อกตรงๆ กล้องจะวิ่งกลับระดับสายตาด้วยความเร็วคงที่ ดูกระตุก
    private bool StartCameraEase()
    {
        if (_cameraEaseTime <= 0f || _camera == null)
            return false;

        Transform cam = _camera.transform;
        float yaw = _camera.CameraRotation.x;
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        _cameraEaseTarget = new GameObject($"{name}_CameraEaseTarget");
        _cameraEaseTarget.transform.position = cam.position + forward * 10f;

        float duration = _cameraEaseTime * _cameraEaseHold;
        if (!_camera.StartForcedLook(_cameraEaseTarget.transform, Vector3.zero, duration + 1f, _cameraEaseTime))
        {
            EndCameraEase();
            return false;
        }

        _cameraEaseLeft = duration;
        return true;
    }

    private void EndCameraEase()
    {
        if (_cameraEaseTarget != null)
        {
            if (_camera != null)
                _camera.StopForcedLook();
            Destroy(_cameraEaseTarget);
        }
        _cameraEaseTarget = null;
        _cameraEaseLeft = 0f;
    }

    // ผู้เล่นติด tag Player หลายตัวลูก — ไล่หาจาก root
    private bool FindPlayer()
    {
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag(_playerTag))
        {
            Transform root = obj.transform.root;
            _flags = root.GetComponentInChildren<StateFlagsAccess>(true);
            _locomotion = root.GetComponentInChildren<BaseLocomotion>(true);
            _camera = root.GetComponentInChildren<PlayerCameraController>(true);
            if (_camera == null)
                _camera = FindAnyObjectByType<PlayerCameraController>();
            if (_flags != null && FindPlayerParts(root))
                return true;
        }
        return false;
    }

    // ===== UI =====

    private void UpdateUI()
    {
        if (_group == null)
            return;

        float dt = Time.unscaledDeltaTime;

        // เมนู / หน้าสอน / Pause เปิดอยู่ = ซ่อนป้าย ไม่ทับเมนู
        UIManager ui = UIManager.Instance;
        bool hidden = ui != null && ui.HasAnyOpen;
        _menuVisible = Mathf.MoveTowards(_menuVisible, hidden ? 0f : 1f, dt * 6f);

        // เฟดเข้าทุกครั้งที่เปลี่ยนขั้น / ทำสำเร็จแล้วเฟดออก
        float fadeTarget = _state == State.FadeOut ? 0f : 1f;
        _fade = Mathf.MoveTowards(_fade, fadeTarget, dt / Mathf.Max(0.01f, _fadeTime));
        _group.alpha = _menuVisible * Mathf.SmoothStep(0f, 1f, _fade);

        UpdateBackground();

        // เต้นตุบๆ แบบป้าย Block — มีรูปปุ่มให้ปุ่มเต้น / ไม่มีให้ตัวหนังสือเต้น
        float target = _state == State.Done || _state == State.FadeOut ? 1f : 1f + _pulse * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * _pulseSpeed));
        _keyScale = Mathf.Lerp(_keyScale, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
        if (_key != null)
            _key.rectTransform.localScale = Vector3.one * _keyScale;
        else
            _label.rectTransform.localScale = Vector3.one * _keyScale;
    }

    // พื้นหลังวงรีหลังรูปปุ่ม — เปิดตามขั้น / ขนาดตามรูปปุ่ม × Background Scale
    // เรียกทุกเฟรมด้วย จะได้ปรับ Background Scale ระหว่างเล่นแล้วเห็นเลย
    private void UpdateBackground()
    {
        UnityEngine.UI.Image background = _view != null && _group == _view.Group ? _view.KeyBackground : null;
        if (background == null)
            return;

        bool show = _stepBackground && _key != null && _key.enabled;
        background.enabled = show;
        if (show)
        {
            Vector2 size = _key.rectTransform.sizeDelta;
            background.rectTransform.sizeDelta = new Vector2(size.x * _backgroundScale.x, size.y * _backgroundScale.y);
        }
    }

    // ใช้ SpellPrompt ใน UIRoot ถ้ามี (หน้าตาแบบป้าย Block) — ไม่มีค่อยสร้างป้ายตัวหนังสือเอง
    private void EnsureUI()
    {
        if (_group != null)
            return;

        if (_view == null)
            _view = FindAnyObjectByType<SpellCastPromptView>(FindObjectsInactive.Include);

        if (_view != null && _view.Group != null && _view.Label != null)
        {
            _view.gameObject.SetActive(true);
            _group = _view.Group;
            _label = _view.Label;
            _key = _view.Key;
            if (_key != null)
                _keyBaseSize = _key.rectTransform.sizeDelta;
            _group.alpha = 0f;
            if (!_viewStyled)
            {
                _viewStyled = true;
                StyleLabel();
            }
        }
        else
        {
            CreateUI();
        }

        _keyScale = 1f;
        _shownText = null;
    }

    private void CreateUI()
    {
        _root = new GameObject($"{name}_Prompt", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(_root, gameObject.scene);

        Canvas canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        _group = _root.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(_root.transform, false);
        var rect = (RectTransform)labelObject.transform;
        rect.anchorMin = _screenPosition;
        rect.anchorMax = _screenPosition;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1400f, 120f);

        _label = labelObject.AddComponent<TextMeshProUGUI>();
        if (_font != null)
            _label.font = _font;
        _label.fontSize = _fontSize;
        _label.fontStyle = (_bold ? FontStyles.Bold : FontStyles.Normal) | (_upperCase ? FontStyles.UpperCase : FontStyles.Normal);
        _label.alignment = TextAlignmentOptions.Center;
        _label.color = Color.white;
        _label.raycastTarget = false;
        StyleLabel();

        _shownText = null;
    }

    // ขอบ + เงา แบบเดียวกับ BlockQTEPromptUI — ทำผ่าน material ของตัวหนังสือตัวนี้ตัวเดียว ไม่แก้ฟอนต์ที่ใช้ร่วมกับ UI อื่น
    private void StyleLabel()
    {
        if (_textOutline > 0f)
        {
            _label.outlineWidth = _textOutline;
            _label.outlineColor = _textOutlineColor;

            // ขอบของ TMP อยู่ครึ่งในครึ่งนอก ฟอนต์ผอมโดนกินจนกลวง — ขยายเนื้อออกเท่ากัน ขอบจะไปอยู่ข้างนอก
            _label.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, _textOutline);
        }

        if (_textShadow)
        {
            Material material = _label.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, _textShadowColor);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, _textShadowSize);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, _textShadowSoftness);
        }

        _label.UpdateMeshPadding();
    }

    private void DestroyUI()
    {
        // ป้ายใน UIRoot ไม่ลบ แค่ซ่อน / ป้ายที่สร้างเองลบทิ้ง
        if (_view != null && _group == _view.Group && _group != null)
        {
            _group.alpha = 0f;
            if (_key != null)
                _key.rectTransform.sizeDelta = _keyBaseSize;   // คืนขนาดเดิมใน prefab
            if (_view.KeyBackground != null)
                _view.KeyBackground.enabled = false;
        }

        if (_root != null)
            Destroy(_root);
        _root = null;
        _group = null;
        _label = null;
        _key = null;
        _stepBackground = false;
        _shownText = null;
    }
}
