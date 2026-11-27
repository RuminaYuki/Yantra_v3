using UnityEngine;

/// <summary>
/// เสียงของผีหนึ่งตัว — แปะบนตัวผี (EnemyController)
/// ใช้ระบบเสียงกลางของเกม: SoundID → SoundLibrary → SoundManager (ผ่าน SoundPooler)
/// ตั้งไฟล์ / ความดัง / ระยะ / priority / mixer ทั้งหมดใน SoundData ของ SoundLibrary
///
/// 1. ก้าวเดิน — จับเองจากเท้า (Auto Steps) หรือให้ PretFootstepShake.On Step / Animation Event เรียก Footstep()
/// 2. ตบพื้น — HandSlamDust ส่งสัญญาณมาเอง
/// 3. โผล่ — On Show Up ของ PretShowUpFeedback เรียก ShowUp()
/// 4. เสียงร้องอื่น ๆ — action "Play Ghost Audio" ใน State ของ table
/// 5. ขยับตัว — เสียงวน Move ดังตามความเร็วที่ตัว / มือ / หัวขยับ (ทำเองอัตโนมัติ)
/// 6. กรีดร้องเป็นช่วง ๆ — Random Voice สุ่มเวลาเอง (เริ่มหลังโผล่ หรือสั่ง StartRandomVoice / StopRandomVoice)
/// 7. หายใจ — เสียงวนติดตัวผี เริ่มหลังโผล่ เบาลงเองตอนกรีดร้อง (สั่ง StartBreath / StopBreath ได้)
/// </summary>
[DisallowMultipleComponent]
public class GhostAudio : MonoBehaviour
{
    [Header("เสียง (SoundID ที่อยู่ใน SoundLibrary) — ว่าง = ไม่มีเสียงนั้น")]
    [SerializeField] private SoundID _step;
    [SerializeField] private SoundID _slam;

    [Tooltip("เสียงตอนผีโผล่ — ต่อกับ On Show Up ของ PretShowUpFeedback")]
    [SerializeField] private SoundID _showUp;

    [Header("ก้าวเดิน")]
    [Tooltip("ติ๊ก = GhostAudio จับเท้ากระแทกพื้นเอง\nเอาติ๊กออก = ให้ตัวอื่นสั่ง Footstep() แทน (On Step ของ PretFootstepShake / Animation Event)")]
    [SerializeField] private bool _autoSteps = true;

    [Tooltip("กระดูกเท้า — ว่าง = หา foot.l / foot.r ในตัวลูกให้เอง")]
    [SerializeField] private Transform[] _feet;

    [Tooltip("เท้าสูงจากพื้นไม่เกินเท่านี้ = นับว่าเหยียบ (เมตร) — ใช้ตอน Auto Steps")]
    [SerializeField, Min(0.01f)] private float _stepHeight = 0.15f;

    [Tooltip("เท้าต้องลงเร็วอย่างน้อยเท่านี้ (เมตร/วินาที) — ใช้ตอน Auto Steps")]
    [SerializeField, Min(0f)] private float _stepMinSpeed = 0.6f;

    [Header("ขยับตัว — เสียงวน ดังตามความเร็วที่ตัวขยับ")]
    [Tooltip("เสียงวนตอนขยับตัว เช่น Pret_Move_Loop — ว่าง = ไม่มีเสียงขยับ")]
    [SerializeField] private SoundID _move;

    [Tooltip("กระดูกที่ใช้วัดความเร็ว — ว่าง = ตัวนี้ + มือ + หัว (หาให้เอง)")]
    [SerializeField] private Transform[] _moveBones;

    [Tooltip("ขยับช้ากว่านี้ (เมตร/วินาที) = เงียบ")]
    [SerializeField, Min(0f)] private float _moveMinSpeed = 0.4f;

    [Tooltip("ขยับเร็วเท่านี้ (เมตร/วินาที) = ดังเต็ม")]
    [SerializeField, Min(0.01f)] private float _moveFullSpeed = 4f;

    [Tooltip("เสียงตามความเร็วไวแค่ไหน — สูง = ไว / ต่ำ = นุ่ม ค่อย ๆ ดังค่อย ๆ เบา")]
    [SerializeField, Min(0.1f)] private float _moveResponse = 3f;

    [Header("กรีดร้องเป็นช่วง ๆ")]
    [Tooltip("เสียงร้องสุ่มเป็นช่วง ๆ เช่น Pret_Scream — ว่าง = ไม่ร้อง")]
    [SerializeField] private SoundID _randomVoice;

    [Tooltip("เว้นระหว่างร้อง สั้นสุด (วินาที)")]
    [SerializeField, Min(1f)] private float _randomVoiceMin = 8f;

    [Tooltip("เว้นระหว่างร้อง นานสุด (วินาที)")]
    [SerializeField, Min(1f)] private float _randomVoiceMax = 20f;

    [Tooltip("ติ๊ก = เริ่มร้องหลังผีโผล่ (ShowUp) เท่านั้น ไม่ร้องตอนยังซ่อนตัว\nเอาติ๊กออก = ร้องตั้งแต่เปิดตัว")]
    [SerializeField] private bool _randomVoiceAfterShowUp = true;

    [Header("หายใจ — เสียงวนติดตัวผี")]
    [Tooltip("เสียงหายใจแบบวน เช่น Pret_Breath_Loop — ว่าง = ไม่หายใจ")]
    [SerializeField] private SoundID _breath;

    [Tooltip("ติ๊ก = เริ่มหายใจหลังผีโผล่ (ShowUp) ไม่หายใจตอนยังซ่อนตัว\nเอาติ๊กออก = หายใจตั้งแต่เปิดตัว")]
    [SerializeField] private bool _breathAfterShowUp = true;

    [Tooltip("ค่อย ๆ ดังขึ้นตอนเริ่ม / เบาลงตอนหยุด (วินาที)")]
    [SerializeField, Min(0f)] private float _breathFade = 1.5f;

    [Tooltip("ตอนกรีดร้อง / ร้องอื่น ๆ ลดเสียงหายใจเหลือเท่านี้ (0-1) — 1 = ไม่ลด")]
    [SerializeField, Range(0f, 1f)] private float _breathDuckWhileVoice = 0.3f;

    [Header("เฟดเสียงร้อง (โผล่ / กรีดร้อง / Play Voice)")]
    [Tooltip("ค่อย ๆ ดังขึ้นตอนเริ่มร้อง (วินาที) — 0 = ดังทันที")]
    [SerializeField, Min(0f)] private float _voiceFadeIn = 0.25f;

    [Tooltip("ค่อย ๆ เบาลงตอนท้ายเสียง และตอนโดนเสียงร้องใหม่มาแทน (วินาที) — 0 = ตัดทันที")]
    [SerializeField, Min(0f)] private float _voiceFadeOut = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool _logPlay;

    private const float StepCooldown = 0.25f;
    private const float EventStepCooldown = 0.12f;   // กัน blend tree ยิง event ซ้อน

    private SFXHandle _voice = SFXHandle.None;
    private SFXHandle _loop = SFXHandle.None;
    private SoundID _loopId;

    private HandSlamDust _slamSource;
    private Vector3[] _footLast;
    private bool[] _footHasLast;
    private float[] _footLastStep;
    private float _lastEventStep = -1f;

    private const float MoveTeleportSpeed = 40f;     // เร็วเกินนี้ = วาร์ป ไม่นับ
    private const float MoveSilentStopTime = 1.5f;   // เงียบนานเท่านี้ = คืนลำโพงเข้า pool
    private SFXHandle _moveHandle = SFXHandle.None;
    private Vector3[] _moveLast;
    private bool _moveHasLast;
    private float _moveLevel;
    private float _moveSilentTime;
    private float _moveNextTry;

    private bool _randomVoiceOn;
    private float _nextRandomVoice;

    private float _voiceEndTime;      // unscaled — ลำโพงนับเวลาแบบ unscaled

    private SFXHandle _breathHandle = SFXHandle.None;
    private bool _breathOn;
    private float _breathLevel = -1f;   // ระดับที่สั่งไปล่าสุด (-1 = ยังไม่ได้สั่ง)
    private float _breathNextTry;
    private bool _voiceTailFading;

    // ---------- เรียกจาก action / UnityEvent ----------

    /// <summary>เสียงร้องครั้งเดียว ติดตามตัวผี — ตัดเสียงร้องที่ค้างอยู่ทิ้ง</summary>
    public void PlayVoice(SoundID id)
    {
        if (id == null || SoundManager.Instance == null) return;

        FadeOutVoice();   // เสียงเก่าค่อย ๆ เบาลง ไม่ตัดฉับ
        _voice = SoundManager.Instance.PlaySFXAttachedTracked(id, transform, out float duration);
        _voiceTailFading = false;
        _voiceEndTime = duration > 0f ? Time.unscaledTime + duration : float.MaxValue;

        if (_voice.IsValid && _voiceFadeIn > 0f)
        {
            _voice.SetVolumeMultiplier(0f);
            _voice.FadeToVolumeMultiplier(1f, Mathf.Min(_voiceFadeIn, duration * 0.4f));
        }
        Log($"ร้อง {id.name}");
    }

    /// <summary>ค่อย ๆ เบาเสียงร้องที่เล่นอยู่จนเงียบ</summary>
    public void FadeOutVoice()
    {
        if (!_voice.IsValid) return;

        if (_voiceFadeOut > 0f) _voice.FadeOutAndStop(_voiceFadeOut);
        else _voice.Stop();
        _voice = SFXHandle.None;
    }

    /// <summary>เสียงตอนผีโผล่ — ต่อกับ On Show Up ของ PretShowUpFeedback</summary>
    public void ShowUp()
    {
        PlayVoice(_showUp);
        if (_randomVoiceAfterShowUp) StartRandomVoice();
        if (_breathAfterShowUp) StartBreath();
    }

    /// <summary>เริ่มหายใจ (ค่อย ๆ ดังขึ้น)</summary>
    public void StartBreath() => _breathOn = true;

    /// <summary>หยุดหายใจ (ค่อย ๆ เบาจนเงียบ)</summary>
    public void StopBreath()
    {
        _breathOn = false;
        if (!_breathHandle.IsValid) return;
        _breathHandle.FadeOutAndStop(_breathFade);
        _breathHandle = SFXHandle.None;
        Log("หยุดหายใจ");
    }

    /// <summary>เริ่มกรีดร้องเป็นช่วง ๆ (ครั้งแรกรอตามเวลาสุ่ม ไม่ร้องทันที)</summary>
    public void StartRandomVoice()
    {
        _randomVoiceOn = true;
        ScheduleRandomVoice();
    }

    /// <summary>หยุดกรีดร้องเป็นช่วง ๆ (เสียงที่ร้องอยู่ปล่อยให้จบเอง)</summary>
    public void StopRandomVoice() => _randomVoiceOn = false;

    /// <summary>เปิดเสียงวน ติดตามตัวผี ค่อย ๆ ดังขึ้น (เช่นตอนจ้อง)</summary>
    public void StartLoop(SoundID id, float fadeTime)
    {
        if (id == null || SoundManager.Instance == null) return;

        if (_loop.IsValid && _loopId == id)
        {
            _loop.FadeToVolumeMultiplier(1f, fadeTime);
            return;
        }

        _loop.Stop();
        _loop = SoundManager.Instance.PlayLoopSFXForeverAttached(id, transform);
        _loopId = id;

        if (!_loop.IsValid) return;
        _loop.SetVolumeMultiplier(0f);
        _loop.FadeToVolumeMultiplier(1f, fadeTime);
        Log($"วน {id.name}");
    }

    /// <summary>ค่อย ๆ เบาเสียงวนจนเงียบ แล้วคืนลำโพงเข้า pool</summary>
    public void StopLoop(float fadeTime)
    {
        if (!_loop.IsValid) return;
        _loop.FadeOutAndStop(fadeTime);
        _loop = SFXHandle.None;
        _loopId = null;
    }

    /// <summary>
    /// เสียงเท้า 1 ก้าว — ใส่ใน On Step ของ PretFootstepShake หรือ Animation Event ชื่อ Footstep
    /// ออกตรงเท้าที่ต่ำที่สุด (เท้าที่เพิ่งเหยียบ)
    /// </summary>
    public void Footstep()
    {
        if (Time.time - _lastEventStep < EventStepCooldown) return;
        _lastEventStep = Time.time;

        Vector3 pos = transform.position;
        float lowest = float.MaxValue;
        foreach (Transform foot in _feet)
            if (foot != null && foot.position.y < lowest) { lowest = foot.position.y; pos = foot.position; }

        if (GroundBelow(pos, out RaycastHit ground)) pos = ground.point;
        PlayStep(pos);
    }

    // ---------- Unity ----------

    private void Awake()
    {
        if (_feet == null || _feet.Length == 0 || System.Array.TrueForAll(_feet, f => f == null))
            _feet = FindBones("foot.l", "foot.r", "foot_l", "foot_r", "leftfoot", "rightfoot");
        _footLast = new Vector3[_feet.Length];
        _footHasLast = new bool[_feet.Length];
        _footLastStep = new float[_feet.Length];

        _slamSource = GetComponent<HandSlamDust>();

        if (_moveBones == null || _moveBones.Length == 0 || System.Array.TrueForAll(_moveBones, b => b == null))
        {
            var bones = new System.Collections.Generic.List<Transform> { transform };
            bones.AddRange(FindBones("hand.l", "hand.r", "hand_l", "hand_r", "lefthand", "righthand", "head.x", "head"));
            _moveBones = bones.ToArray();
        }
        _moveLast = new Vector3[_moveBones.Length];
    }

    private void OnEnable()
    {
        if (_slamSource != null) _slamSource.Slammed += OnSlam;
        if (_footHasLast != null)
            for (int i = 0; i < _footHasLast.Length; i++) _footHasLast[i] = false;

        _moveHasLast = false;
        _moveLevel = 0f;
        _moveSilentTime = 0f;

        _randomVoiceOn = !_randomVoiceAfterShowUp;
        if (_randomVoiceOn) ScheduleRandomVoice();

        _breathOn = !_breathAfterShowUp;   // เปิดตัวแล้วหายใจเลย — รอ SoundManager พร้อมใน LateUpdate
        _breathLevel = -1f;
    }

    private void OnDisable()
    {
        if (_slamSource != null) _slamSource.Slammed -= OnSlam;

        // เสียงวน / เสียงติดตามไม่คืนลำโพงเอง — ไม่หยุด = เสียโควต้า pool ถาวร
        _voice.Stop();
        _loop.Stop();
        _moveHandle.Stop();
        _breathHandle.Stop();
        _breathHandle = SFXHandle.None;
        _voice = SFXHandle.None;
        _loop = SFXHandle.None;
        _moveHandle = SFXHandle.None;
        _loopId = null;
    }

    private void LateUpdate()
    {
        if (_autoSteps) UpdateAutoSteps();
        UpdateMove();
        UpdateRandomVoice();
        UpdateVoiceTail();
        UpdateBreath();
    }

    // ---------- หายใจ ----------

    private void UpdateBreath()
    {
        if (!_breathOn || _breath == null || SoundManager.Instance == null) return;

        if (!_breathHandle.IsValid)
        {
            if (Time.time < _breathNextTry) return;
            _breathNextTry = Time.time + 1f;   // pool เต็ม ไม่ต้องลองทุกเฟรม

            _breathHandle = SoundManager.Instance.PlayLoopSFXForeverAttached(_breath, transform);
            if (!_breathHandle.IsValid) return;
            _breathHandle.SetVolumeMultiplier(0f);
            _breathLevel = -1f;
            Log($"เริ่มหายใจ {_breath.name}");
        }

        // กรีดร้องอยู่ = หายใจเบาลง (ร้องกับหายใจพร้อมกันไม่ได้)
        float target = _voice.IsPlaying ? _breathDuckWhileVoice : 1f;
        if (Mathf.Approximately(target, _breathLevel)) return;

        float fade = _breathLevel < 0f ? _breathFade : 0.3f;   // ครั้งแรกค่อย ๆ มา / หลบเสียงร้องไว ๆ
        _breathHandle.FadeToVolumeMultiplier(target, fade);
        _breathLevel = target;
    }

    // ---------- เฟดท้ายเสียงร้อง ----------

    // ไฟล์เสียงบางไฟล์จบแบบตัดฉับ — ค่อย ๆ เบาลงก่อนถึงท้ายไฟล์
    private void UpdateVoiceTail()
    {
        if (_voiceTailFading || _voiceFadeOut <= 0f || !_voice.IsValid) return;

        float left = _voiceEndTime - Time.unscaledTime;
        if (left > _voiceFadeOut) return;

        _voice.FadeToVolumeMultiplier(0f, Mathf.Max(0.01f, left));
        _voiceTailFading = true;
    }

    // ---------- กรีดร้องเป็นช่วง ๆ ----------

    private void UpdateRandomVoice()
    {
        if (!_randomVoiceOn || _randomVoice == null || SoundManager.Instance == null) return;
        if (Time.time < _nextRandomVoice) return;

        // เสียงร้องอื่นยังเล่นอยู่ (เช่นเสียงโผล่) — รออีกนิด ไม่ร้องทับ
        if (_voice.IsPlaying)
        {
            _nextRandomVoice = Time.time + 1f;
            return;
        }

        PlayVoice(_randomVoice);
        ScheduleRandomVoice();
    }

    private void ScheduleRandomVoice()
    {
        float max = Mathf.Max(_randomVoiceMin, _randomVoiceMax);
        _nextRandomVoice = Time.time + UnityEngine.Random.Range(_randomVoiceMin, max);
    }

    // ---------- ขยับตัว ----------

    private void UpdateMove()
    {
        if (_move == null || SoundManager.Instance == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float speed = MeasureMoveSpeed(dt);
        float target = Mathf.InverseLerp(_moveMinSpeed, _moveFullSpeed, speed);
        _moveLevel = Mathf.Lerp(_moveLevel, target, 1f - Mathf.Exp(-_moveResponse * dt));

        if (_moveLevel > 0.02f)
        {
            _moveSilentTime = 0f;
            if (!_moveHandle.IsValid)
            {
                if (Time.time < _moveNextTry) return;
                _moveNextTry = Time.time + 0.5f;   // pool เต็ม ไม่ต้องลองทุกเฟรม
                _moveHandle = SoundManager.Instance.PlayLoopSFXForeverAttached(_move, transform);
                if (!_moveHandle.IsValid) return;
                Log($"เริ่มเสียงขยับ {_move.name}");
            }
            _moveHandle.SetVolumeMultiplier(_moveLevel);
            return;
        }

        if (!_moveHandle.IsValid) return;
        _moveHandle.SetVolumeMultiplier(_moveLevel);
        _moveSilentTime += dt;
        if (_moveSilentTime < MoveSilentStopTime) return;

        _moveHandle.Stop();
        _moveHandle = SFXHandle.None;
        Log("หยุดเสียงขยับ (นิ่งนาน)");
    }

    /// <summary>ความเร็วของกระดูกที่ขยับเร็วที่สุดในเฟรมนี้ (เมตร/วินาที)</summary>
    private float MeasureMoveSpeed(float dt)
    {
        float fastest = 0f;
        for (int i = 0; i < _moveBones.Length; i++)
        {
            Transform bone = _moveBones[i];
            if (bone == null) continue;

            Vector3 pos = bone.position;
            if (_moveHasLast)
            {
                float speed = (pos - _moveLast[i]).magnitude / dt;
                if (speed < MoveTeleportSpeed && speed > fastest) fastest = speed;
            }
            _moveLast[i] = pos;
        }
        _moveHasLast = true;
        return fastest;
    }

    // ---------- ก้าวเดิน (Auto) ----------

    private void UpdateAutoSteps()
    {
        if (_step == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        for (int i = 0; i < _feet.Length; i++)
        {
            Transform foot = _feet[i];
            if (foot == null) continue;

            Vector3 pos = foot.position;
            if (!_footHasLast[i])
            {
                _footLast[i] = pos;
                _footHasLast[i] = true;
                continue;
            }

            float downSpeed = (_footLast[i].y - pos.y) / dt;
            _footLast[i] = pos;

            if (downSpeed < _stepMinSpeed) continue;
            if (Time.time - _footLastStep[i] < StepCooldown) continue;
            if (!GroundBelow(pos, out RaycastHit ground)) continue;
            if (pos.y - ground.point.y > _stepHeight) continue;

            _footLastStep[i] = Time.time;
            PlayStep(ground.point);
        }
    }

    private void PlayStep(Vector3 point)
    {
        if (_step == null || SoundManager.Instance == null) return;
        if (!CreatureFootstepBudget.TryConsume()) return;   // ผีหลายตัวเดินพร้อมกัน ไม่ให้เสียงเท้าล้น

        SoundManager.Instance.PlaySFX(_step, point);
        Log($"ก้าว {_step.name}");
    }

    private bool GroundBelow(Vector3 pos, out RaycastHit ground)
    {
        ground = default;
        foreach (RaycastHit hit in Physics.RaycastAll(pos + Vector3.up * 0.5f, Vector3.down, 2f,
                                                      ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (ground.collider == null || hit.distance < ground.distance) ground = hit;
        }
        return ground.collider != null;
    }

    // ---------- ตบพื้น ----------

    private void OnSlam(Vector3 point)
    {
        if (_slam == null || SoundManager.Instance == null) return;
        SoundManager.Instance.PlaySFX(_slam, point);
        Log($"ตบ {_slam.name}");
    }

    // ---------- Helpers ----------

    private Transform[] FindBones(params string[] names)
    {
        var found = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (System.Array.IndexOf(names, t.name.ToLowerInvariant()) >= 0) found.Add(t);
        return found.ToArray();
    }

    private void Log(string message)
    {
        if (_logPlay) Debug.Log($"[GhostAudio] {name} {message}", this);
    }
}
