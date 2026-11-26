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

    // ---------- เรียกจาก action / UnityEvent ----------

    /// <summary>เสียงร้องครั้งเดียว ติดตามตัวผี — ตัดเสียงร้องที่ค้างอยู่ทิ้ง</summary>
    public void PlayVoice(SoundID id)
    {
        if (id == null || SoundManager.Instance == null) return;

        _voice.Stop();
        _voice = SoundManager.Instance.PlaySFXAttachedTracked(id, transform, out _);
        Log($"ร้อง {id.name}");
    }

    /// <summary>เสียงตอนผีโผล่ — ต่อกับ On Show Up ของ PretShowUpFeedback</summary>
    public void ShowUp() => PlayVoice(_showUp);

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
    }

    private void OnEnable()
    {
        if (_slamSource != null) _slamSource.Slammed += OnSlam;
        if (_footHasLast != null)
            for (int i = 0; i < _footHasLast.Length; i++) _footHasLast[i] = false;
    }

    private void OnDisable()
    {
        if (_slamSource != null) _slamSource.Slammed -= OnSlam;

        // เสียงวน / เสียงติดตามไม่คืนลำโพงเอง — ไม่หยุด = เสียโควต้า pool ถาวร
        _voice.Stop();
        _loop.Stop();
        _voice = SFXHandle.None;
        _loop = SFXHandle.None;
        _loopId = null;
    }

    private void LateUpdate()
    {
        if (_autoSteps) UpdateAutoSteps();
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
