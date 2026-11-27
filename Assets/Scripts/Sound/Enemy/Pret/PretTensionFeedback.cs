using UnityEngine;

/// <summary>
/// ให้ผู้เล่นรู้ตัวว่าเปรตใกล้จะตบ — อ่านมาตรวัด PretDetectComponent แล้ว
///   1. หัวใจเต้น — ยิ่งมาตรสูง ยิ่งดังและยิ่งเร็ว
///      เล่น "ตุบ" กับ "ตับ" แยกกัน ระบบวางจังหวะเอง — ยิ่งเร็ว ตุบ→ตับ ยิ่งชิด เหมือนหัวใจจริง
///      เสียงใหม่มา เสียงเก่าค่อย ๆ เงียบ ไม่ซ้อนกันเละ
///   2. กล้องสั่นเบา ๆ ตอนมาตรใกล้เต็ม (ใช้ CameraShaker ของเกม)
/// มาตรลด (ผู้เล่นหลบสายตา / เปรตกลับบ้าน) ทุกอย่างก็ค่อย ๆ หายเอง
///
/// แปะบนตัวเปรต (EnemyController) — ไม่แก้ไฟล์ของทีม แค่อ่านค่า
/// </summary>
[DisallowMultipleComponent]
public class PretTensionFeedback : MonoBehaviour
{
    [Header("มาตรวัด")]
    [Tooltip("ว่าง = หาในตัวนี้ / ตัวแม่ / ตัวลูกให้เอง")]
    [SerializeField] private PretDetectComponent _detect;

    [Tooltip("มาตรถึงเท่านี้ เริ่มรู้สึก")]
    [SerializeField, Min(0f)] private float _startAt = 60f;

    [Tooltip("มาตรถึงเท่านี้ = แรงสุด — ตั้งเท่า AttackDetect_Value ใน table (ตอนนี้ 170)")]
    [SerializeField, Min(0.01f)] private float _fullAt = 170f;

    [Tooltip("ตามมาตรไวแค่ไหน — สูง = ไว / ต่ำ = นุ่ม ค่อย ๆ มาค่อย ๆ ไป")]
    [SerializeField, Min(0.1f)] private float _response = 2f;

    [Header("1. หัวใจเต้น (ยิ่งใกล้ ยิ่งดัง ยิ่งเร็ว)")]
    [Tooltip("เสียง \"ตุบ\" (จังหวะแรก) — SoundData ตั้ง Spatial Blend = 0\nถ้าช่อง Dub ว่าง ใส่ไฟล์ตุบ-ตับในไฟล์เดียวก็ได้")]
    [SerializeField] private SoundID _heartbeat;

    [Tooltip("เสียง \"ตับ\" (จังหวะสอง) — ว่าง = ไม่มี (ใช้ไฟล์ตุบ-ตับในช่องบน)")]
    [SerializeField] private SoundID _heartbeatDub;

    [Tooltip("ตุบ → ตับ ห่างกันกี่วินาที ตอนหัวใจช้า")]
    [SerializeField, Min(0.05f)] private float _gapSlow = 0.30f;

    [Tooltip("ตุบ → ตับ ห่างกันกี่วินาที ตอนหัวใจเร็วสุด — หัวใจจริงยิ่งเร็วยิ่งชิด")]
    [SerializeField, Min(0.05f)] private float _gapFast = 0.18f;

    [Tooltip("ความดังของ \"ตับ\" เทียบกับ \"ตุบ\"")]
    [SerializeField, Range(0f, 1f)] private float _dubVolume = 0.8f;

    [Tooltip("เสียงใหม่มาแล้ว เสียงเก่าค่อย ๆ เงียบในกี่วินาที — กันเสียงซ้อนกันจนฟังเหมือนหัวใจแย่งกันเต้น")]
    [SerializeField, Min(0f)] private float _overlapFade = 0.06f;

    [Tooltip("จังหวะตอนเริ่มรู้สึก (ครั้งต่อนาที) — คนปกติ ~70")]
    [SerializeField, Min(20f)] private float _minBpm = 70f;

    [Tooltip("จังหวะตอนมาตรเต็ม (ครั้งต่อนาที) — ตกใจสุดขีด ~150")]
    [SerializeField, Min(20f)] private float _maxBpm = 150f;

    [SerializeField, Range(0f, 1f)] private float _minVolume = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _maxVolume = 1f;

    [Tooltip("ความแรงต่ำกว่านี้ (0-1) = หัวใจไม่เต้น")]
    [SerializeField, Range(0f, 0.9f)] private float _heartbeatFrom = 0.05f;

    [Header("2. กล้องสั่นเบา ๆ ตอนใกล้โดนตบ")]
    [Tooltip("แรงสั่นตอนมาตรเต็ม (trauma 0-1) — 0 = ไม่สั่น\n0.3 = สั่นเบา ๆ แบบพื้นสะเทือน")]
    [SerializeField, Range(0f, 1f)] private float _rumbleMaxTrauma = 0.3f;

    [Tooltip("เริ่มสั่นเมื่อความแรงเกินเท่านี้ (0-1) — 0.6 = สั่นแค่ช่วงท้าย ๆ")]
    [SerializeField, Range(0f, 0.99f)] private float _rumbleFrom = 0.6f;

    [Header("Debug")]
    [Tooltip("โชว์ใน Play Mode")]
    [SerializeField, Range(0f, 1f)] private float _tension;

    [SerializeField] private bool _log;

    private float _beatPhase;   // 0 → 1 = ครบ 1 ครั้ง
    private float _dubIn = -1f; // นับถอยหลังถึง "ตับ" (-1 = ไม่มีรอ)
    private float _dubLevel;
    private SFXHandle _lastBeat = SFXHandle.None;

    /// <summary>ความแรงตอนนี้ 0-1 เผื่อระบบอื่นอยากใช้</summary>
    public float Tension => _tension;

    private void Awake()
    {
        if (_detect == null) _detect = GetComponentInParent<PretDetectComponent>();
        if (_detect == null) _detect = GetComponentInChildren<PretDetectComponent>(true);
        if (_detect == null)
            Debug.LogWarning($"[PretTension] {name} หา PretDetectComponent ไม่เจอ — ลากใส่ช่อง Detect เอง", this);
    }

    private void OnEnable()
    {
        _tension = 0f;
        _beatPhase = 0.9f;
        _dubIn = -1f;
    }

    private void OnDisable()
    {
        _lastBeat.Stop();
        _lastBeat = SFXHandle.None;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // pause

        float target = 0f;
        if (_detect != null)
            target = Mathf.InverseLerp(_startAt, Mathf.Max(_startAt + 0.01f, _fullAt), _detect.DetectPoint);

        _tension = Mathf.Lerp(_tension, target, 1f - Mathf.Exp(-_response * dt));
        if (_tension < 0.001f) _tension = 0f;

        UpdateHeartbeat(dt);
        UpdateRumble();
    }

    // ---------- 1. หัวใจเต้น ----------

    private void UpdateHeartbeat(float dt)
    {
        if (_heartbeat == null || SoundManager.Instance == null) return;

        // "ตับ" ที่รอคิวอยู่ — เล่นต่อให้จบแม้มาตรจะลดลงแล้ว
        if (_dubIn >= 0f)
        {
            _dubIn -= dt;
            if (_dubIn < 0f) PlayOne(_heartbeatDub, _dubLevel);
        }

        if (_tension < _heartbeatFrom)
        {
            _beatPhase = 0.9f;   // กลับมารู้สึกอีกที = เต้นครั้งแรกเกือบทันที
            return;
        }

        float k = Mathf.InverseLerp(_heartbeatFrom, 1f, _tension);
        float bpm = Mathf.Lerp(_minBpm, Mathf.Max(_minBpm, _maxBpm), k);

        // นับเป็น phase แทนนับถอยหลัง — จังหวะเร่งได้ลื่น ๆ ไม่สะดุดตอน bpm เปลี่ยนกลางคัน
        _beatPhase += dt * bpm / 60f;
        if (_beatPhase < 1f) return;
        _beatPhase -= Mathf.Floor(_beatPhase);

        float volume = Mathf.Lerp(_minVolume, _maxVolume, k);
        PlayOne(_heartbeat, volume);

        if (_heartbeatDub != null)
        {
            // ตุบ→ตับ ห่างไม่เกินครึ่งจังหวะ ไม่งั้นตับจะไปชิดตุบถัดไปจนจับจังหวะไม่ได้
            float gap = Mathf.Lerp(_gapSlow, _gapFast, k);
            _dubIn = Mathf.Min(gap, 0.5f * 60f / bpm);
            _dubLevel = volume * _dubVolume;
        }

        if (_log) Debug.Log($"[PretTension] ตุบ {bpm:F0} bpm ดัง {volume:F2}", this);
    }

    private void PlayOne(SoundID id, float volume)
    {
        if (id == null || SoundManager.Instance == null) return;

        // เสียงก่อนหน้ายังดังอยู่ = ค่อย ๆ เงียบ ไม่ซ้อนกัน
        if (_lastBeat.IsPlaying)
        {
            if (_overlapFade > 0f) _lastBeat.FadeOutAndStop(_overlapFade);
            else _lastBeat.Stop();
        }

        // เสียงหัวใจของริน — ติดไว้ที่หูผู้เล่น (ตั้ง Spatial Blend = 0 อยู่แล้ว ตำแหน่งแค่กันเผื่อ)
        Transform ear = AudioListenerCache.Transform;
        if (ear == null && Camera.main != null) ear = Camera.main.transform;
        if (ear == null) ear = transform;

        _lastBeat = SoundManager.Instance.PlaySFXAttachedTracked(id, ear, out _);
        _lastBeat.SetVolumeMultiplier(volume);
    }

    // ---------- 2. กล้องสั่น ----------

    // เติม trauma ให้ไม่ต่ำกว่าเป้า — CameraShaker ลดเองทุกเฟรม เราแค่คอยเติมให้ค้างไว้
    private void UpdateRumble()
    {
        if (_rumbleMaxTrauma <= 0f || CameraShaker.Instance == null) return;

        float k = Mathf.InverseLerp(_rumbleFrom, 1f, _tension);
        float floor = k * _rumbleMaxTrauma;
        float current = CameraShaker.Instance.Trauma;

        if (floor > current)
            CameraShaker.Instance.AddTrauma(floor - current);
    }
}
