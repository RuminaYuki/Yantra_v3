using UnityEngine;
using Yantra.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// เสียง "แก๊ก" ตอนกดยิงแต่กระสุนหมด — กดกี่ทีก็ดังทุกที
// วางบนตัวผู้เล่น (ตัวไหนก็ได้ที่อยู่ใต้ผู้เล่น เช่นตัวเดียวกับ GunController)
// ไม่ไปยุ่งกับ state machine — แค่ฟังปุ่มยิง แล้วดูว่าถือปืนอยู่และไม่มีกระสุน
public class DryFireSound : MonoBehaviour
{
    [Header("เสียง")]
    [Tooltip("SoundID ของเสียงแก๊ก — แนะนำเปิด Use Random Pitch ใน SoundData ให้แต่ละทีไม่ซ้ำกันเป๊ะ")]
    [SerializeField] private SoundID _clickSound;

    [Tooltip("เสียงเกิดที่ไหน — เว้นว่าง = ที่ตัวปืนในมือ")]
    [SerializeField] private Transform _origin;

    [Tooltip("กดรัวแค่ไหนก็เว้นอย่างน้อยเท่านี้วินาที กันเสียงซ้อนจนแตก")]
    [SerializeField, Min(0f)] private float _minInterval = 0.12f;

#if ENABLE_INPUT_SYSTEM
    [Header("ปุ่มยิง")]
    [Tooltip("Action ยิงจาก Input Actions ของโปรเจกต์ (เช่น Player/Fire)\nเว้นว่าง = คลิกซ้าย หรือไกขวาของจอย")]
    [SerializeField] private InputActionReference _fireAction;
#endif

    [Header("ปืน + กระสุน (เว้นว่าง = หาเอง)")]
    [SerializeField] private GunController _gun;
    [SerializeField] private PlayerInventory _inventory;

    [Tooltip("ItemData ของกระสุน — ตัวเดียวกับที่ใส่ใน WeaponHUD")]
    [SerializeField] private ItemData _bulletItem;

    [Tooltip("ชื่อ Object ปืนในมือที่โดนเปิด/ปิดตอนชัก/เก็บปืน — ชื่อเดียวกับใน WeaponHUD")]
    [SerializeField] private string _heldObjectName = "R_GUN";

    [Header("ยังไม่ได้รีโหลด (ไม่ใช้ก็ได้)")]
    [Tooltip("ติ๊ก = มีกระสุนในกระเป๋าแต่ยังไม่ได้บรรจุ กดยิงก็ดังแก๊กด้วย\nไม่ติ๊ก = ดังเฉพาะตอนกระสุนหมดกระเป๋า")]
    [SerializeField] private bool _clickWhenNotLoaded = false;

    [Tooltip("Flag เดียวกับใน WeaponHUD เช่น MushReload_Flag")]
    [SerializeField] private FlagSO _reloadFlag;

    [Tooltip("ตั้งให้เหมือนใน WeaponHUD")]
    [SerializeField] private bool _flagTrueMeansLoaded = true;

    [SerializeField] private StateFlagsAccess _stateFlags;

    [Header("กันดังผิดจังหวะ")]
    [Tooltip("ดังเฉพาะตอนเมาส์ถูกล็อกอยู่ (กำลังเล่นจริง) — เปิดเมนู/กระเป๋าแล้วคลิกจะไม่ดัง")]
    [SerializeField] private bool _onlyWhenCursorLocked = true;

    [Header("Debug")]
    [SerializeField] private bool _logClick;

    private GameObject _heldObject;
    private PlayerCameraController _camera;
    private float _lastClick = -999f;

#if ENABLE_INPUT_SYSTEM
    private bool _enabledAction;
#endif

    private void Awake()
    {
        FindGun();
        if (_camera == null) _camera = transform.root.GetComponentInChildren<PlayerCameraController>(true);
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        // ถ้าโปรเจกต์ใช้คลาส C# ที่ generate จาก Input Actions ตัว action ใน asset จะไม่ได้ถูกเปิด
        // เปิดเองไว้อ่านปุ่มเฉย ๆ ไม่กระทบระบบยิงเดิม
        if (_fireAction != null && _fireAction.action != null && !_fireAction.action.enabled)
        {
            _fireAction.action.Enable();
            _enabledAction = true;
        }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (_enabledAction && _fireAction != null && _fireAction.action != null)
            _fireAction.action.Disable();
        _enabledAction = false;
#endif
    }

    private void Update()
    {
        if (!FirePressed()) return;
        if (!CanClick()) return;
        if (Time.unscaledTime - _lastClick < _minInterval) return;

        _lastClick = Time.unscaledTime;
        PlayClick();
    }

    private bool CanClick()
    {
        if (Time.timeScale <= 0f) return false;
        if (StateMachineController.IsPaused) return false;
        if (_onlyWhenCursorLocked && Cursor.lockState != CursorLockMode.Locked) return false;

        var ui = UIManager.Instance;
        if (ui != null && ui.HasAnyOpen) return false;

        if (_camera != null && _camera.IsCutsceneMode) return false;

        if (!IsHoldingGun()) return false;

        int reserve = ReadCount();
        if (reserve <= 0) return true;                 // หมดกระเป๋า
        return _clickWhenNotLoaded && !IsLoaded();     // ยังไม่ได้บรรจุ
    }

    private void PlayClick()
    {
        if (_logClick)
            Debug.Log($"[DryFire] แก๊ก — {(_clickSound != null ? _clickSound.name : "ยังไม่ได้ใส่เสียง")}", this);

        if (_clickSound == null || SoundManager.Instance == null) return;

        Transform origin = _origin != null ? _origin
            : _heldObject != null ? _heldObject.transform
            : _gun != null ? _gun.transform
            : transform;

        SoundManager.Instance.PlaySFXAttached(_clickSound, origin);
    }

    private bool FirePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (_fireAction != null && _fireAction.action != null)
            return _fireAction.action.WasPressedThisFrame();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    // ---------- หาปืน / กระสุน แบบเดียวกับ WeaponHUD ----------

    private void FindGun()
    {
        Transform root = transform.root;

        if (_inventory == null) _inventory = root.GetComponentInChildren<PlayerInventory>(true);
        if (_inventory == null) _inventory = FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (_gun == null) _gun = root.GetComponentInChildren<GunController>(true);
        if (_gun == null && _inventory != null) _gun = _inventory.transform.root.GetComponentInChildren<GunController>(true);

        if (_heldObject == null && _gun != null && !string.IsNullOrEmpty(_heldObjectName))
        {
            // หาเฉพาะใต้ปืนในมือ — ปืนที่วางบนพื้นก็มี Object ชื่อเดียวกัน
            foreach (Transform t in _gun.GetComponentsInChildren<Transform>(true))
                if (t.name == _heldObjectName) { _heldObject = t.gameObject; break; }
        }

        if (_stateFlags == null && _reloadFlag != null)
        {
            foreach (StateFlags f in root.GetComponentsInChildren<StateFlags>(true))
                if (f.Contains(_reloadFlag)) { _stateFlags = f; break; }
        }

        if (_bulletItem == null)
            Debug.LogWarning("[DryFire] ยังไม่ได้ใส่ Bullet Item — จะนับว่ากระสุนหมดตลอด", this);
    }

    private bool IsHoldingGun()
    {
        if (_heldObject == null && _gun == null) FindGun();
        if (_heldObject != null) return _heldObject.activeInHierarchy;
        return _gun != null && _gun.isActiveAndEnabled;
    }

    private int ReadCount()
    {
        if (_inventory == null || _inventory.Inventory == null || _bulletItem == null) return 0;
        return _inventory.Inventory.CountOf(_bulletItem);
    }

    private bool IsLoaded()
    {
        if (_reloadFlag == null || _stateFlags == null) return true;
        bool value = _stateFlags.Get(_reloadFlag);
        return _flagTrueMeansLoaded ? value : !value;
    }
}
