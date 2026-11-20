using System.Reflection;
using UnityEngine;

// ไฟแฟลชปากกระบอกตอนยิง — แสงวาบสั้นๆ ส่องรอบตัว (ยิงในที่มืด ผนัง / พื้น / มือ สว่างวาบ)
//
// วิธีใช้:
//   1) คลิกขวา MuzzlePoint > Light > Point Light (ตั้งชื่อ MuzzleFlashLight)
//   2) ใส่สคริปต์นี้ที่ Point Light ตัวนั้น
//   3) ปรับสี / Intensity / Range ที่ตัว Light ได้เลย — ค่าที่ตั้ง = ตอนสว่างสุด สคริปต์ปิดไฟไว้แล้วเปิดแวบเดียวตอนยิง
//
// ฟังจังหวะยิงจาก Anim Event ตัวเดียวกับ GunController (อ่านค่าจาก GunController ให้เอง) — ไม่แก้ไฟล์ปืนของทีม
// ยิงโหมดพิเศษ (กระสุนอาคม) วาบแรงกว่า นานกว่า สีต่างจากปกติ
[DisallowMultipleComponent]
public class MuzzleFlashLight : MonoBehaviour
{
    [Header("อ้างอิง (เว้นว่าง = หาเอง)")]
    [SerializeField] private Light _light;
    [SerializeField] private GunController _gun;

    [Tooltip("เว้นว่าง = ใช้ตัวเดียวกับที่ GunController ใช้")]
    [SerializeField] private AnimEventDispatcher _dispatcher;

    [Tooltip("เว้นว่าง = ใช้ Event Key เดียวกับที่ GunController ใช้")]
    [SerializeField] private string _eventKey = "";

    [Header("แฟลชปกติ")]
    [Tooltip("วาบนานกี่วินาที — สว่างสุดทันที แล้วค่อยๆ มืดลง")]
    [SerializeField, Min(0.01f)] private float _duration = 0.12f;

    [Tooltip("สุ่มความแรงแต่ละนัด ± กี่ % — ทุกนัดจะได้ไม่เหมือนกันเป๊ะ")]
    [SerializeField, Range(0f, 0.5f)] private float _randomness = 0.15f;

    [Header("โหมดพิเศษ (กระสุนอาคม)")]
    [Tooltip("ปิด = โหมดพิเศษวาบเหมือนปกติ")]
    [SerializeField] private bool _specialLook = true;
    [SerializeField] private Color _specialColor = new Color(1f, 0.3f, 0.25f);

    [Tooltip("สว่างกว่าปกติกี่เท่า")]
    [SerializeField, Min(0f)] private float _specialIntensity = 2.5f;

    [SerializeField, Min(0.01f)] private float _specialDuration = 0.3f;

    [Header("ไม่วาบตอนไม่มีกระสุน")]
    [Tooltip("ติ๊ก = กระสุนในกระเป๋าหมด ท่ายิงเล่นแต่ไม่มีลูกออก ไฟจะไม่วาบ\nปืนที่ตั้งไม่ใช้กระสุนจากกระเป๋า สคริปต์รู้เอง วาบทุกนัด")]
    [SerializeField] private bool _needAmmo = true;

    [Header("Debug")]
    [SerializeField] private bool _logFlash;

    private bool _subscribed;
    private float _peakIntensity;
    private Color _normalColor;
    private bool _gunUsesAmmo = true;
    private float _ammoAtFrameStart;

    private bool _flashing;
    private float _flashTime;
    private float _flashDuration;
    private float _flashPeak;

    private void Awake()
    {
        if (_light == null) _light = GetComponent<Light>();
        if (_light == null) _light = GetComponentInChildren<Light>(true);
        if (_light == null)
        {
            Debug.LogWarning($"[MuzzleFlash] {name} ไม่มี Light — สร้าง Point Light แล้วใส่สคริปต์นี้ที่ตัวไฟ", this);
            enabled = false;
            return;
        }

        // ค่าที่ตั้งไว้ในตัว Light = ตอนสว่างสุด
        _peakIntensity = _light.intensity;
        _normalColor = _light.color;
        _light.enabled = false;

        if (_gun == null) _gun = GetComponentInParent<GunController>();
        if (_gun == null) _gun = transform.root.GetComponentInChildren<GunController>(true);
        if (_gun == null) _gun = FindAnyObjectByType<GunController>();

        if (_gun != null)
        {
            // ค่าใน GunController เป็น private — อ่านเอาเอง จะได้ไม่ต้องไปแก้ไฟล์ปืน / ไม่ต้องตั้งซ้ำ
            if (_dispatcher == null) _dispatcher = ReadField<AnimEventDispatcher>(_gun, "dispatcher");
            if (string.IsNullOrEmpty(_eventKey)) _eventKey = ReadField<string>(_gun, "eventKey");

            FieldInfo useItem = FindField(_gun, "useItemBullet");
            if (useItem != null && useItem.GetValue(_gun) is bool uses) _gunUsesAmmo = uses;
        }

        if (_dispatcher == null || string.IsNullOrEmpty(_eventKey))
            Debug.LogWarning($"[MuzzleFlash] หาจังหวะยิงไม่เจอ — ลาก AnimEventDispatcher กับพิมพ์ Event Key ให้เหมือนใน GunController", this);
    }

    private void OnEnable()
    {
        if (_dispatcher == null || string.IsNullOrEmpty(_eventKey)) return;

        // เรียกแบบเดียวกับ GunController
        _dispatcher.GetEvent(_eventKey).AddListener(OnShoot);
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (_subscribed && _dispatcher != null)
            _dispatcher.GetEvent(_eventKey).RemoveListener(OnShoot);
        _subscribed = false;

        _flashing = false;
        if (_light != null) _light.enabled = false;
    }

    private void Update()
    {
        // จำจำนวนกระสุนก่อน Anim Event ของเฟรมนี้ — ปืนหักกระสุนตอน Event ถ้าเช็คทีหลังนัดสุดท้ายจะไม่วาบ
        if (_gun != null) _ammoAtFrameStart = _gun.GetCurrentBullet();

        if (!_flashing) return;

        _flashTime += Time.deltaTime;
        if (_flashTime >= _flashDuration)
        {
            _flashing = false;
            _light.enabled = false;
            return;
        }

        // สว่างสุดทันที แล้วมืดลงเร็วตอนแรก ช้าตอนท้าย
        float k = 1f - _flashTime / _flashDuration;
        _light.intensity = _flashPeak * k * k;
    }

    private void OnShoot()
    {
        if (_light == null) return;

        if (_needAmmo && _gunUsesAmmo && _gun != null &&
            Mathf.Max(_ammoAtFrameStart, _gun.GetCurrentBullet()) <= 0f)
        {
            if (_logFlash) Debug.Log("[MuzzleFlash] กระสุนหมด — ไม่วาบ", this);
            return;
        }

        bool special = _specialLook && _gun != null && _gun.GetGunMode() == GunController.GunMode.Special;
        Flash(special);
    }

    /// <summary>สั่งวาบเอง — เช่นผูกกับ UnityEvent อื่น</summary>
    public void Flash(bool special)
    {
        if (_light == null) return;

        float random = 1f + Random.Range(-_randomness, _randomness);
        _flashPeak = _peakIntensity * (special ? _specialIntensity : 1f) * random;
        _flashDuration = special ? _specialDuration : _duration;
        _flashTime = 0f;
        _flashing = true;

        _light.color = special ? _specialColor : _normalColor;
        _light.intensity = _flashPeak;
        _light.enabled = true;

        if (_logFlash) Debug.Log($"[MuzzleFlash] วาบ{(special ? " (โหมดพิเศษ)" : "")}", this);
    }

    private static FieldInfo FindField(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static T ReadField<T>(object target, string field) where T : class
    {
        FieldInfo info = FindField(target, field);
        return info != null ? info.GetValue(target) as T : null;
    }
}
