using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yantra.UI;

// ป้ายบังคับร่ายเวทครั้งแรก (แบบป้าย "กด F ค้าง" ของ Block) — ขึ้นหลังปิดหน้าสอนเวท
// ระหว่างนี้ล็อกเดิน / ต่อย / กัน / ปืน เหลือแค่เวท จนผู้เล่นร่ายสำเร็จ 1 ครั้ง
//
// ขั้น: กดคลิกขวาค้าง (วงเวทเปิด) → เลือกแล้วปล่อย (ได้ยันต์) → วาดยันต์ → คลิกซ้ายปล่อยพลัง → จบ
// ปล่อยคลิกขวาโดยไม่ได้เลือก / ยันต์หาย = กลับไปขั้นแรก
//
// ฟังแค่ event channel ของระบบเวทที่มีอยู่แล้ว ไม่ได้แก้ไฟล์ระบบเวท
// เริ่ม: ต่อ Start Channel กับ On Closed ของหน้าสอน (Tut_Yan) / หรือเรียก Begin() เอง
public class SpellCastPrompt : MonoBehaviour
{
    private enum Step { Idle, WaitSpellFlag, Hold, Select, Draw, Cast, Done }

    [Header("เริ่มเมื่อ")]
    [Tooltip("ช่องเดียวกับ On Closed ของหน้าสอนเวท — ปิดหน้าสอนแล้วป้ายขึ้นทันที")]
    [SerializeField] private VoidEventChannelSO _startChannel;

    [Header("ฟังระบบเวท (ช่องเดิมของระบบ)")]
    [Tooltip("Bool_RadialSO_EventChannel — วงเวทเปิด (true) / ปิด (false)")]
    [SerializeField] private BoolRadialSOAdvEventChannal _radialChannel;

    [Tooltip("PlayerOnFinishedSpellDraw_VEC — วาดยันต์เสร็จ")]
    [SerializeField] private VoidEventChannelSO _drawFinishedChannel;

    [Tooltip("EffectFinished_VEC — ปล่อยพลังเสร็จ = จบ")]
    [SerializeField] private VoidEventChannelSO _castFinishedChannel;

    [Header("ล็อกระหว่างบังคับ")]
    [Tooltip("PlayerEnableSpell — ปิดอยู่ = วงเวทเปิดไม่ได้ ป้ายจะไม่ขึ้น (กันผู้เล่นค้าง)")]
    [SerializeField] private FlagSO _spellFlag;

    [Tooltip("ปิดหน้าสอนแล้ว flag เวทยังไม่เปิด (ท่าเก็บของยังไม่จบ) — รอได้นานสุดกี่วินาที (เวลาในเกม) เกินแล้วยกเลิก")]
    [SerializeField, Min(0f)] private float _waitForSpellFlag = 5f;

    [Tooltip("flag ที่ปิดชั่วคราว (ต่อย / กัน / ปืน) — จบแล้วคืนค่าเดิม")]
    [SerializeField] private FlagSO[] _lockFlags;

    [SerializeField] private bool _lockMovement = true;

    [Tooltip("ล็อกการหันกล้อง (เหมือนตอนวงเวทขึ้น) ตั้งแต่ป้ายขึ้นจนร่ายเสร็จ — เลือกเวท / วาดยันต์ใช้เมาส์ได้ปกติ")]
    [SerializeField] private bool _lockCamera = true;

    [Tooltip("กล้องค่อยๆ หันกลับระดับสายตาก่อนล็อก ใช้เวลาประมาณกี่วินาที — มากขึ้น = นุ่มขึ้น / 0 = ล็อกทันที (กล้องวิ่งกลับแบบความเร็วคงที่)")]
    [SerializeField, Min(0f)] private float _cameraEaseTime = 0.35f;

    [Tooltip("ช่วงนุ่มนานกี่เท่าของ Camera Ease Time ก่อนล็อกจริง — 2.5 = เข้าใกล้ระดับสายตาเกือบสุดแล้วค่อยล็อก")]
    [SerializeField, Min(1f)] private float _cameraEaseHold = 2.5f;

    [Tooltip("ร่ายครั้งนี้ไม่หักของ — กันผู้เล่นไม่มีวัตถุดิบแล้วค้างอยู่ขั้นแรก")]
    [SerializeField] private bool _freeCast = true;

    [Header("ข้อความ — รูปปุ่มเมาส์โชว์แยกที่ Key Icon ของ SpellPrompt (UIRoot)")]
    [Tooltip("ใส่ {RMB} {LMB} ในข้อความได้ — แต่ถ้าใช้ป้ายใน UIRoot รูปปุ่มจะซ้ำกับ Key Icon")]
    [SerializeField] private string _holdText = "HOLD TO OPEN YOUR SPELLS";
    [SerializeField] private string _selectText = "MOVE TO A SPELL, THEN RELEASE";
    [SerializeField] private string _drawText = "MOVE THE MOUSE ALONG THE LINE";
    [SerializeField] private string _castText = "CLICK TO CAST";
    [Tooltip("ว่าง = ไม่โชว์ จบแล้วหายเลย")]
    [SerializeField] private string _doneText = "";
    [SerializeField, Min(0f)] private float _doneTime = 1f;

    [Header("รูปปุ่มแต่ละขั้น — โชว์ที่ Key Icon ของ SpellPrompt (UIRoot) / ว่าง = ไม่มีรูป")]
    [SerializeField] private Sprite _holdIcon;
    [SerializeField] private Sprite _selectIcon;
    [SerializeField] private Sprite _drawIcon;
    [SerializeField] private Sprite _castIcon;

    [Header("พื้นหลังรูปปุ่ม (KeyBackground ใน SpellPrompt) — สี / ความเข้ม ปรับที่ Color ของ Image")]
    [SerializeField] private bool _holdBackground;
    [SerializeField] private bool _selectBackground = true;
    [SerializeField] private bool _drawBackground;
    [SerializeField] private bool _castBackground;
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

    [SerializeField] private string _playerTag = "Player";

    private const int SortingOrder = 450;   // ต่ำกว่าแถบดำคัตซีน / เมนู

    private Step _step;
    private int _checkTemplateFrame = -1;
    private float _doneTimer;
    private float _waitTimer;

    private StateFlagsAccess _flags;
    private BaseLocomotion _locomotion;
    private PlayerCameraController _camera;
    private GameObject _cameraEaseTarget;
    private float _cameraEaseLeft;
    private SpellController _spell;
    private readonly Dictionary<FlagSO, bool> _savedFlags = new();
    private bool _locked;
    private bool _savedIgnoreCost;

    private GameObject _root;          // ป้ายที่สร้างเอง (ไม่มี SpellPrompt ใน UIRoot)
    private SpellCastPromptView _view;  // ป้ายใน UIRoot
    private bool _viewStyled;
    private CanvasGroup _group;
    private TMP_Text _label;
    private UnityEngine.UI.Image _key;
    private float _keyScale = 1f;
    private Vector2 _keyBaseSize = new Vector2(80f, 80f);
    private string _shownText;

    public bool IsRunning => _step != Step.Idle;

    private void OnEnable()
    {
        if (_startChannel != null) _startChannel.Raised += Begin;
        if (_radialChannel != null) _radialChannel.Raised += HandleRadial;
        if (_drawFinishedChannel != null) _drawFinishedChannel.Raised += HandleDrawFinished;
        if (_castFinishedChannel != null) _castFinishedChannel.Raised += HandleCastFinished;
    }

    // ปิด Domain Reload ไว้ — ไม่ถอดออก Play รอบหน้า channel จะยังเรียกตัวเก่า
    // ถูกปิดกลางคัน (เปลี่ยนซีน / ตาย) — ปลดล็อกคืนทุกอย่าง ไม่งั้นผู้เล่นต่อยไม่ได้ถาวร
    private void OnDisable()
    {
        if (_startChannel != null) _startChannel.Raised -= Begin;
        if (_radialChannel != null) _radialChannel.Raised -= HandleRadial;
        if (_drawFinishedChannel != null) _drawFinishedChannel.Raised -= HandleDrawFinished;
        if (_castFinishedChannel != null) _castFinishedChannel.Raised -= HandleCastFinished;

        Unlock();
        _step = Step.Idle;
        DestroyUI();
    }

    [ContextMenu("Begin (ทดสอบ)")]
    public void Begin()
    {
        if (_step != Step.Idle)
            return;

        if (!FindPlayer())
        {
            Debug.LogWarning($"[SpellCastPrompt] {name}: หาผู้เล่น (StateFlags / SpellController) ไม่เจอ — ไม่บังคับ", this);
            return;
        }

        // flag เวทเปิดตอนท่าเก็บของจบ — ซึ่งช้ากว่าตอนปิดหน้าสอน รอก่อน ยังไม่ล็อก / ไม่ขึ้นป้าย
        if (_spellFlag != null && !_flags.Get(_spellFlag))
        {
            _step = Step.WaitSpellFlag;
            _waitTimer = _waitForSpellFlag;
            return;
        }

        StartPrompt();
    }

    private void StartPrompt()
    {
        Lock();
        SetStep(Step.Hold);
    }

    // เวลาในเกม — หยุดนับตอนหน้าสอน / Pause เปิด (ท่าเก็บของก็หยุดตามด้วย)
    private void UpdateWaitSpellFlag()
    {
        if (_flags != null && _flags.Get(_spellFlag))
        {
            StartPrompt();
            return;
        }

        _waitTimer -= Time.deltaTime;
        if (_waitTimer <= 0f)
        {
            _step = Step.Idle;
            Debug.LogWarning($"[SpellCastPrompt] {name}: รอ {_spellFlag.name} เปิดเกิน {_waitForSpellFlag:0.#} วิ — ไม่บังคับ (กันผู้เล่นค้าง)", this);
        }
    }

    // ===== ขั้นตอน (ฟังจากระบบเวท) =====

    private void HandleRadial(bool open, RadialMenuDataSO data)
    {
        if (open && _step == Step.Hold)
            SetStep(Step.Select);
        else if (!open && _step == Step.Select)
            _checkTemplateFrame = Time.frameCount + 1;   // ยันต์สร้างตอนปิดวง — รอเฟรมถัดไปค่อยเช็ค
    }

    private void HandleDrawFinished()
    {
        if (_step == Step.Draw)
            SetStep(Step.Cast);
    }

    private void HandleCastFinished()
    {
        if (_step == Step.Draw || _step == Step.Cast)
            Finish();
    }

    private void Update()
    {
        if (_step == Step.Idle)
            return;

        if (_step == Step.WaitSpellFlag)
        {
            UpdateWaitSpellFlag();
            return;
        }

        // ปล่อยคลิกขวาแล้ว — ได้ยันต์ = ไปวาด / ไม่ได้เลือก = กลับไปกดใหม่
        if (_checkTemplateFrame >= 0 && Time.frameCount >= _checkTemplateFrame)
        {
            _checkTemplateFrame = -1;
            SetStep(_spell != null && _spell.GetHaveTemplat ? Step.Draw : Step.Hold);
        }

        // ยันต์หายกลางทางโดยไม่ได้วาดเสร็จ — กลับไปเริ่มใหม่
        if (_step == Step.Draw && _spell != null && !_spell.GetHaveTemplat)
            SetStep(Step.Hold);

        if (_step == Step.Done)
        {
            _doneTimer -= Time.unscaledDeltaTime;
            if (_doneTimer <= 0f)
            {
                _step = Step.Idle;
                DestroyUI();
                return;
            }
        }

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
        // state เวทปลดล็อกกล้องตอนออก (เช่น ปิดวงเวท) — ล็อกซ้ำทุกเฟรมจนร่ายเสร็จ
        else if (_locked && _lockCamera && _camera != null && !_camera.IsLookLocked)
        {
            _camera.IsLookLocked = true;
        }

        UpdateUI();
    }

    private void SetStep(Step step)
    {
        _step = step;
        string text = step switch
        {
            Step.Hold => _holdText,
            Step.Select => _selectText,
            Step.Draw => _drawText,
            Step.Cast => _castText,
            Step.Done => _doneText,
            _ => "",
        };

        EnsureUI();
        if (_shownText != text)
        {
            _shownText = text;
            InputIconText.Apply(_label, text);
        }

        // รูปปุ่มของขั้นนี้ (ตั้งใน Inspector) — ช่องว่าง = ซ่อนรูป
        if (_key != null)
        {
            Sprite sprite = step switch
            {
                Step.Hold => _holdIcon,
                Step.Select => _selectIcon,
                Step.Draw => _drawIcon,
                Step.Cast => _castIcon,
                _ => null,
            };
            _key.enabled = sprite != null;
            if (sprite != null)
            {
                _key.sprite = sprite;

                // รูปกว้าง (เช่น กดค้าง → ปล่อย) — ความสูงเท่าเดิม ขยายกว้างตามสัดส่วนรูป ไม่ให้โดนบีบ
                float aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
                _key.rectTransform.sizeDelta = new Vector2(_keyBaseSize.y * aspect, _keyBaseSize.y);
            }
        }

        UpdateBackground();
    }

    // พื้นหลังวงรีหลังรูปปุ่ม — เปิดตามขั้นที่ติ๊กไว้ / ขนาดตามรูปปุ่ม × Background Scale
    // เรียกทุกเฟรมด้วย จะได้ปรับ Background Scale ระหว่างเล่นแล้วเห็นเลย
    private void UpdateBackground()
    {
        UnityEngine.UI.Image background = _view != null && _group == _view.Group ? _view.KeyBackground : null;
        if (background == null)
            return;

        bool show = _key != null && _key.enabled && _step switch
        {
            Step.Hold => _holdBackground,
            Step.Select => _selectBackground,
            Step.Draw => _drawBackground,
            Step.Cast => _castBackground,
            _ => false,
        };
        background.enabled = show;
        if (show)
        {
            Vector2 size = _key.rectTransform.sizeDelta;
            background.rectTransform.sizeDelta = new Vector2(size.x * _backgroundScale.x, size.y * _backgroundScale.y);
        }
    }

    private void Finish()
    {
        Unlock();

        if (!string.IsNullOrEmpty(_doneText) && _doneTime > 0f)
        {
            _doneTimer = _doneTime;
            SetStep(Step.Done);
            return;
        }

        _step = Step.Idle;
        DestroyUI();
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

        if (_freeCast)
        {
            _savedIgnoreCost = RadialMenuButtonData.IgnoreItemCost;
            RadialMenuButtonData.IgnoreItemCost = true;
        }
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
        if (_lockCamera && _camera != null)
            _camera.IsLookLocked = false;

        if (_freeCast)
            RadialMenuButtonData.IgnoreItemCost = _savedIgnoreCost;
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
            _spell = root.GetComponentInChildren<SpellController>(true);
            _locomotion = root.GetComponentInChildren<BaseLocomotion>(true);
            _camera = root.GetComponentInChildren<PlayerCameraController>(true);
            if (_camera == null)
                _camera = FindAnyObjectByType<PlayerCameraController>();
            if (_flags != null && _spell != null)
                return true;
        }
        return false;
    }

    // ===== UI (สร้างเอง ไม่ต้องทำ prefab) =====

    private void UpdateUI()
    {
        if (_group == null)
            return;

        // เมนู / หน้าสอน / Pause เปิดอยู่ = ซ่อนป้าย ไม่ทับเมนู
        UIManager ui = UIManager.Instance;
        bool hidden = ui != null && ui.HasAnyOpen;
        _group.alpha = Mathf.MoveTowards(_group.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime * 6f);

        UpdateBackground();

        // เต้นตุบๆ แบบป้าย Block — มีรูปปุ่มให้ปุ่มเต้น / ไม่มีให้ตัวหนังสือเต้น
        float target = _step == Step.Done ? 1f : 1f + _pulse * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * _pulseSpeed));
        _keyScale = Mathf.Lerp(_keyScale, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
        if (_key != null)
            _key.rectTransform.localScale = Vector3.one * _keyScale;
        else
            _label.rectTransform.localScale = Vector3.one * _keyScale;
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
        _shownText = null;
    }
}
