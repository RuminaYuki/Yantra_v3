using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD ปืนแบบ RE4 — วงกระสุน, เลขกระสุน, วงบรรจุใหม่หลังยิง
// เก็บปืน = สลับเป็นรูปหมัดแป๊บนึงแล้วจางหาย
public class WeaponHUD : MonoBehaviour
{
    [Header("ข้อมูล (เว้นว่าง = หาเอง)")]
    [SerializeField] private GunController _gun;
    [SerializeField] private PlayerInventory _inventory;
    [Tooltip("ItemData ของกระสุน — ตัวเดียวกับ Item Bullet ใน GunController")]
    [SerializeField] private ItemData _bulletItem;
    [Tooltip("Object ปืนที่โดนเปิด/ปิดตอนชัก/เก็บปืน (เช่น R_GUN)\nเว้นว่าง = ดูว่า GunController เปิดอยู่ไหม")]
    [SerializeField] private GameObject _heldObject;
    [Tooltip("ถ้าลากใส่ช่องบนไม่ได้ (อยู่คนละ Prefab) ใส่ชื่อแทน โค้ดจะหาใต้ GunController ให้ตอนเริ่มเกม")]
    [SerializeField] private string _heldObjectName = "R_GUN";
    [Tooltip("วงเต็มเมื่อมีกระสุนเท่านี้")]
    [SerializeField, Min(1)] private int _capacity = 20;
    [Tooltip("เหลือเท่านี้หรือน้อยกว่า = สีเตือน")]
    [SerializeField, Min(0)] private int _lowAmmo = 3;

    [Header("UI")]
    [SerializeField] private CanvasGroup _group;
    [Tooltip("Image แบบ Filled / Radial 360 — วงกระสุน")]
    [SerializeField] private Image _ammoRing;
    [Tooltip("Image แบบ Filled / Radial 360 — วงเล็กที่วิ่งเติมหลังยิง (ใส่หรือไม่ใส่ก็ได้)")]
    [SerializeField] private Image _readyRing;
    [SerializeField] private Image _weaponIcon;
    [SerializeField] private TMP_Text _ammoText;
    [SerializeField] private TMP_Text _capacityText;
    [Tooltip("ขึ้นคำว่า SPECIAL ตอนเปลี่ยนโหมดปืน (ใส่หรือไม่ใส่ก็ได้)")]
    [SerializeField] private TMP_Text _modeText;

    [Header("ตอนเก็บปืน")]
    [Tooltip("รูปหมัด — เว้นว่าง = จางหายเฉยๆ ไม่สลับรูป")]
    [SerializeField] private Sprite _fistIcon;
    [Tooltip("โชว์รูปหมัดค้างไว้กี่วินาทีก่อนจางหาย")]
    [SerializeField, Min(0f)] private float _fistShowTime = 1f;

    [Header("สี")]
    [SerializeField] private Color _normalColor = new Color(0.45f, 0.85f, 0.5f);
    [SerializeField] private Color _lowColor = new Color(1f, 0.65f, 0.2f);
    [SerializeField] private Color _emptyColor = new Color(0.9f, 0.25f, 0.2f);
    [SerializeField] private Color _specialColor = new Color(0.75f, 0.5f, 1f);
    [SerializeField] private Color _textColor = Color.white;

    [Header("จังหวะ")]
    [Tooltip("เวลาวงเล็กวิ่งเติมหลังยิง — ใส่ให้เท่า Delay Time ของปืน")]
    [SerializeField, Min(0.01f)] private float _readyTime = 1f;
    [Tooltip("0 = โชว์ตลอดที่ถือปืน / มากกว่า 0 = ไม่ได้ยิงนานเท่านี้วินาทีแล้วค่อยๆ จางไป")]
    [SerializeField, Min(0f)] private float _hideAfterIdle = 0f;
    [SerializeField, Min(0.1f)] private float _fadeSpeed = 4f;
    [SerializeField, Min(1f)] private float _punchScale = 1.3f;
    [SerializeField, Min(0.01f)] private float _punchTime = 0.18f;

    private int _count = -1;
    private float _shownFill;
    private float _readyTimer;
    private float _punchTimer;
    private float _lastActivity = -999f;
    private float _holsterTime = -999f;
    private float _ammoAlpha;
    private bool _wasHolding;
    private Sprite _gunIcon;
    private Color _capacityColor = Color.white;

    private void Start()
    {
        SetGunComponents();

        if (_heldObject == null)
            Debug.LogWarning($"[WeaponHUD] หา '{_heldObjectName}' ใต้ {(_gun != null ? _gun.name : "ปืน")} ไม่เจอ — HUD จะดูจาก GunController แทน", this);
        if (_weaponIcon != null) _gunIcon = _weaponIcon.sprite;
        if (_capacityText != null)
        {
            _capacityText.text = "/" + _capacity;
            _capacityColor = _capacityText.color;
        }
        if (_group != null) _group.alpha = 0f;
        _wasHolding = IsHolding();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;   // ยังขยับตอน HitStop
        float now = Time.unscaledTime;
        int count = ReadCount();

        if (count != _count)
        {
            if (_count >= 0)
            {
                if (count < _count) _readyTimer = _readyTime;   // จำนวนลด = เพิ่งยิง
                _punchTimer = _punchTime;
                _lastActivity = now;
            }
            _count = count;
            if (_ammoText != null) _ammoText.text = count.ToString();
        }

        bool holding = IsHolding();
        if (holding != _wasHolding)
        {
            if (!holding) _holsterTime = now;
            else _lastActivity = now;
            _wasHolding = holding;
        }

        // ตัวกรอบ HUD: ถือปืน = โชว์ / เพิ่งเก็บ = โชว์รูปหมัดแป๊บนึง / นอกนั้นจางหาย
        bool awake = _hideAfterIdle <= 0f || now - _lastActivity < _hideAfterIdle;
        bool showFist = !holding && _fistIcon != null && now - _holsterTime < _fistShowTime;
        float groupTarget = (holding && awake) || showFist ? 1f : 0f;
        if (_group != null) _group.alpha = Mathf.MoveTowards(_group.alpha, groupTarget, _fadeSpeed * dt);

        // วงกับตัวเลขหายก่อนทันทีที่เก็บปืน หมัดไม่มีกระสุนให้นับ
        _ammoAlpha = Mathf.MoveTowards(_ammoAlpha, holding ? 1f : 0f, _fadeSpeed * 2f * dt);

        if (_weaponIcon != null)
        {
            bool fist = !holding && _fistIcon != null;
            _weaponIcon.sprite = fist ? _fistIcon : _gunIcon;
            _weaponIcon.color = !fist && count <= 0 ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        }

        bool special = _gun != null && _gun.GetGunMode() == GunController.GunMode.Special;
        Color ringColor = special ? _specialColor : count <= 0 ? _emptyColor : count <= _lowAmmo ? _lowColor : _normalColor;

        _shownFill = Mathf.MoveTowards(_shownFill, Mathf.Clamp01((float)count / _capacity), 2f * dt);
        if (_ammoRing != null)
        {
            _ammoRing.fillAmount = _shownFill;
            _ammoRing.color = WithAlpha(ringColor, _ammoAlpha);
        }

        _readyTimer = Mathf.Max(0f, _readyTimer - dt);
        if (_readyRing != null)
        {
            _readyRing.fillAmount = 1f - _readyTimer / _readyTime;
            _readyRing.color = WithAlpha(ringColor, (_readyTimer > 0f ? 0.9f : 0.35f) * _ammoAlpha);
        }

        if (_ammoText != null)
        {
            Color textColor = count <= 0 ? _emptyColor : count <= _lowAmmo ? _lowColor : _textColor;
            _ammoText.color = WithAlpha(textColor, _ammoAlpha);
            _punchTimer = Mathf.Max(0f, _punchTimer - dt);
            float t = _punchTimer / _punchTime;
            _ammoText.transform.localScale = Vector3.one * Mathf.Lerp(1f, _punchScale, t * t);
        }

        if (_capacityText != null)
            _capacityText.color = WithAlpha(_capacityColor, _capacityColor.a * _ammoAlpha);

        if (_modeText != null)
        {
            _modeText.enabled = special;
            _modeText.color = WithAlpha(_specialColor, _ammoAlpha);
        }
    }

    private void SetGunComponents()
    {
        if (_inventory == null) _inventory = FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (_gun == null && _inventory != null) _gun = _inventory.transform.root.GetComponentInChildren<GunController>(true);
        if (_gun == null) _gun = FindAnyObjectByType<GunController>(FindObjectsInactive.Include);
        if (_heldObject == null && !string.IsNullOrEmpty(_heldObjectName) && _gun != null)
        {
            // หาเฉพาะใต้ปืนในมือ — ปืนที่วางบนพื้นก็มี Object ชื่อเดียวกัน
            foreach (Transform t in _gun.GetComponentsInChildren<Transform>(true))
                if (t.name == _heldObjectName) { _heldObject = t.gameObject; break; }
        }
    }

    private bool IsHolding()
    {
        if (_heldObject == null && _gun == null) SetGunComponents();
        if (_heldObject != null) return _heldObject.activeInHierarchy;
        return _gun != null && _gun.isActiveAndEnabled;
    }

    private int ReadCount()
    {
        if (_inventory == null || _inventory.Inventory == null || _bulletItem == null) return 0;
        return _inventory.Inventory.CountOf(_bulletItem);
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}