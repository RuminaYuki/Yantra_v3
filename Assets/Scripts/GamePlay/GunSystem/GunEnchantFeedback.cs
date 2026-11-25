using UnityEngine;

// ออร่าอาคมรอบปืน — SP เต็มเมื่อไหร่ ออร่าค่อยๆ ขึ้น / SP ลดลง ออร่าค่อยๆ หาย
//
// วางไว้ที่ GameObject "Gun" (ตัวเดียวกับ GunController) แล้วลาก GameObject "Enchant"
// (ที่ใส่ GunEnchant.mat) มาใส่ช่อง Aura — ตอนออร่าหายหมด สคริปต์ปิดแค่ Renderer ไม่ได้ปิด GameObject
public class GunEnchantFeedback : MonoBehaviour
{
    [Header("อ้างอิง (เว้นว่าง = หาเอง)")]
    [Tooltip("Renderer ของออร่า (GameObject Enchant)")]
    [SerializeField] private Renderer _aura;

    [Tooltip("SkillPoints ของผู้เล่น — เว้นว่าง = หาจากตัวผู้เล่น")]
    [SerializeField] private SkillPoints _skillPoints;

    [Header("เงื่อนไข")]
    [Tooltip("SP ต้องถึงกี่ % ของ Max ถึงขึ้นออร่า (1 = เต็มเท่านั้น)")]
    [SerializeField, Range(0.1f, 1f)] private float _fullRatio = 1f;

    [Header("จังหวะ")]
    [Tooltip("ออร่าขึ้นเต็มในกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _fadeInTime = 0.35f;

    [Tooltip("ออร่าหายหมดในกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _fadeOutTime = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool _log;

    private static readonly int EnchantId = Shader.PropertyToID("_Enchant");

    private Material _material;   // instance ของออร่าตัวนี้ — ไม่ไปแก้ไฟล์ .mat
    private float _value;
    private bool _wasFull;
    private float _nextLookupTime;

    private void Awake()
    {
        if (_aura == null)
        {
            Debug.LogWarning($"[GunEnchant] {name} ยังไม่ได้ลาก Renderer ของออร่า (GameObject Enchant) ใส่ช่อง Aura", this);
            enabled = false;
            return;
        }

        _material = _aura.material;   // สร้าง instance ของตัวเอง
        Apply(0f);
    }

    private void OnDisable()
    {
        // ปืนถูกเก็บ / ปิด — ออร่าหายทันที
        _value = 0f;
        _wasFull = false;
        if (_material != null) Apply(0f);
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    private void Update()
    {
        bool full = IsSkillPointFull();

        if (_log && full != _wasFull)
            Debug.Log($"[GunEnchant] SP {(full ? "เต็ม — ขึ้นออร่า" : "ไม่เต็ม — ปิดออร่า")}", this);
        _wasFull = full;

        float target = full ? 1f : 0f;
        if (Mathf.Approximately(_value, target)) return;

        float time = full ? _fadeInTime : _fadeOutTime;
        _value = Mathf.MoveTowards(_value, target, Time.deltaTime / time);
        Apply(_value);
    }

    private bool IsSkillPointFull()
    {
        if (_skillPoints == null)
        {
            // หาไม่เจอ → รอ 1 วิค่อยหาใหม่ (ผู้เล่นอาจ spawn ทีหลัง)
            if (Time.unscaledTime < _nextLookupTime) return false;
            _nextLookupTime = Time.unscaledTime + 1f;

            _skillPoints = GetComponentInParent<SkillPoints>();
            if (_skillPoints == null) _skillPoints = transform.root.GetComponentInChildren<SkillPoints>(true);
            if (_skillPoints == null) return false;
        }

        float max = _skillPoints.MaxSkillPoints;
        if (max <= 0f) return false;

        // เผื่อทศนิยมคลาด (decay / clamp)
        return _skillPoints.CurrentSkillPoints >= max * _fullRatio - 0.001f;
    }

    private void Apply(float value)
    {
        _material.SetFloat(EnchantId, value);

        // ออร่าหายหมด = ปิด Renderer ไปเลย ไม่ต้องวาดเปลือกที่มองไม่เห็น
        bool visible = value > 0.001f;
        if (_aura.enabled != visible) _aura.enabled = visible;
    }
}
