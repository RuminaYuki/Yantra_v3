using UnityEngine;

/// <summary>
/// ช่องเสียงตามท่า — table กลางที่ผีหลายตัวใช้ร่วมกัน (เช่น table ลูกน้อง) สั่งตามช่อง
/// ผีแต่ละตัวเลือกเองว่าช่องไหนใช้ไฟล์อะไร (GhostAudio > เสียงตามท่า)
/// ตัวเลขกำหนดเอง: Unity เซฟเป็นตัวเลข แทรกช่องใหม่แล้วของเก่าไม่เพี้ยน
/// </summary>
public enum GhostSoundSlot
{
    None = 0,
    Alert = 1,       // เห็นผู้เล่น
    Attack = 2,      // ตี
    Hurt = 3,        // โดนตี
    HurtHeavy = 4,   // โดนหนัก / ล้มลงรอ Glory
    Death = 5,       // ตาย
    Spawn = 6,       // โผล่ตัว
    GloryIdle = 7,   // เสียงวนตอนล้มรอ Glory
}

[System.Serializable]
public class GhostSlotSound
{
    public GhostSoundSlot Slot;
    public SoundID Sound;

    [Tooltip("รอกี่วินาทีค่อยเล่น — ให้ตรงจังหวะท่าของผีตัวนี้ (บวกกับ Delay ใน action)")]
    [Min(0f)] public float Delay;
}

/// <summary>เสียงตาม state ใน Animator — เข้า / วน / ออก (ใช้กับบอสที่มี table ของตัวเอง ไม่ต้องแก้ table)</summary>
[System.Serializable]
public class GhostStateSound
{
    [Tooltip("ชื่อ state ใน Animator (layer แรก) เช่น Jump_ShowDown — ต้องสะกดตรงเป๊ะ")]
    public string State;

    [Tooltip("ดังตอนเข้า state")]
    public SoundID Enter;

    [Tooltip("วนตลอดที่อยู่ใน state — ออกแล้วค่อย ๆ เงียบ")]
    public SoundID Loop;

    [Tooltip("ดังตอนออกจาก state")]
    public SoundID Exit;

    [Tooltip("เข้า state แล้วรอกี่วินาทีค่อยดัง Enter — ออกจาก state ก่อน = ไม่ดัง")]
    [Min(0f)] public float Delay;

    [Tooltip("ติ๊ก = ดังแค่ครั้งแรกครั้งเดียว (เช่น เสียงเปิดตัวบอส)")]
    public bool OnlyFirstTime;

    [System.NonSerialized] public int Hash;
    [System.NonSerialized] public bool Pending;
    [System.NonSerialized] public bool Played;
}

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
/// 8. เสียงตามท่า — table กลางสั่งตามช่อง (เห็นผู้เล่น / ตี / โดนตี / ตาย ...) ผีตัวนี้เลือกไฟล์เอง
/// </summary>
[DisallowMultipleComponent]
public class GhostAudio : MonoBehaviour
{
    [Header("เสียง (SoundID ที่อยู่ใน SoundLibrary) — ว่าง = ไม่มีเสียงนั้น")]
    [SerializeField] private SoundID _step;
    [SerializeField] private SoundID _slam;

    [Tooltip("เสียงตอนผีโผล่ — ต่อกับ On Show Up ของ PretShowUpFeedback")]
    [SerializeField] private SoundID _showUp;

    [Tooltip("เสียงฟาด / ขว้าง — ดังตอนท่าโจมตีถึง Animation Event CastAttack (ตีใกล้) หรือ ShootProjectile (ขว้างของ)\nเฟรมเดียวกับที่ทำดาเมจ / ยิง — โดนหรือวืดก็ดัง / ว่าง = ไม่มีเสียง")]
    [SerializeField] private SoundID _swing;

    [Tooltip("เสียงตอนท่าโจมตีโดนผู้เล่นจริง (จาก AttackSphereCast บนตัวเดียวกัน)\nผู้เล่นกันหรือปัดได้ = ไม่ดัง / ว่าง = ไม่มีเสียง")]
    [SerializeField] private SoundID _hit;

    [Tooltip("เสียงเลือด / เนื้อ — ใส่ Animation Event ชื่อ GhostGore ตรงเฟรมที่ต้องการ (เช่น กระสือล้วงคอ ควักเนื้อ)\nออกตรงคอ / หัว / ว่าง = ไม่มีเสียง")]
    [SerializeField] private SoundID _gore;

    [Header("ก้าวเดิน")]
    [Tooltip("ติ๊ก = GhostAudio จับเท้ากระแทกพื้นเอง\nเอาติ๊กออก = ใส่ Animation Event ชื่อ GhostStep ในท่าเดินเอง (หรือให้ On Step ของ PretFootstepShake สั่ง Footstep())")]
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

    [Header("เสียงตามท่า — table กลางสั่งตามช่อง ผีตัวนี้เลือกไฟล์เอง")]
    [Tooltip("ใส่เฉพาะช่องที่ผีตัวนี้มีเสียง — ช่องที่ไม่ใส่ = เงียบ\n" +
             "Alert เห็นผู้เล่น / Attack ตี / Hurt โดนตี / HurtHeavy ล้มรอ Glory / Death ตาย / GloryIdle วนตอนรอ Glory")]
    [SerializeField] private GhostSlotSound[] _slots = new GhostSlotSound[0];

    [Header("เสียงตาม state ใน Animator — เข้า / วน / ออก (ไม่ต้องใส่ event / ไม่ต้องแก้ table)")]
    [Tooltip("ใส่ชื่อ state ใน Animator กับเสียง — state เดียวใส่ได้หลายแถว (เช่น แถวเสียงเปิดตัว Only First Time)")]
    [SerializeField] private GhostStateSound[] _stateSounds = new GhostStateSound[0];

    [Header("ร้องเจ็บตอนโดนดาเมจ (บอสที่ไม่ได้ใช้ table ลูกน้อง)")]
    [Tooltip("ร้องตอนโดนดาเมจ เว้นช่วงตาม Cooldown — ว่าง = ไม่ใช้\nลูกน้องใช้ช่อง Hurt ในเสียงตามท่าแทน ไม่ต้องใส่ตรงนี้")]
    [SerializeField] private SoundID _damageHurt;

    [Tooltip("ร้องแล้วเว้นอย่างน้อยกี่วินาที (สุ่ม ±20%) — โดนยิงรัว ๆ จะได้ไม่ร้องรัว")]
    [SerializeField, Min(0f)] private float _damageHurtCooldown = 2.5f;

    [Header("เฟดเสียงร้อง (โผล่ / กรีดร้อง / Play Voice)")]
    [Tooltip("ค่อย ๆ ดังขึ้นตอนเริ่มร้อง (วินาที) — 0 = ดังทันที")]
    [SerializeField, Min(0f)] private float _voiceFadeIn = 0.25f;

    [Tooltip("ค่อย ๆ เบาลงตอนท้ายเสียง และตอนโดนเสียงร้องใหม่มาแทน (วินาที) — 0 = ตัดทันที")]
    [SerializeField, Min(0f)] private float _voiceFadeOut = 0.4f;

    [Header("พุ่งชน — ดูจาก state ใน Animator (ไม่ต้องใส่ event / ไม่ต้องแก้ table)")]
    [Tooltip("ชื่อ state ท่าพุ่งใน Animator เช่น Charge — ว่าง = ผีตัวนี้ไม่พุ่ง")]
    [SerializeField] private string _chargeState = "";

    [Tooltip("ดังตอนเริ่มพุ่ง (ร้อง / ออกตัว) — ติดตัวผีไปด้วย")]
    [SerializeField] private SoundID _chargeStart;

    [Tooltip("เสียงวนระหว่างพุ่ง (วิ่งหนัก ๆ)")]
    [SerializeField] private SoundID _chargeLoop;

    [Tooltip("ดังตอนออกจากท่าพุ่ง (เบรก / หยุด)")]
    [SerializeField] private SoundID _chargeStop;

    [Tooltip("พุ่งชนโดนผู้เล่น — ใช้แทน Hit ตอนกำลังพุ่ง / ว่าง = ใช้ Hit ปกติ")]
    [SerializeField] private SoundID _chargeHit;

    [Header("Debug")]
    [SerializeField] private bool _logPlay;

    private const float StepCooldown = 0.25f;
    private const float EventStepCooldown = 0.12f;   // กัน blend tree ยิง event ซ้อน
    private const float EventMinWeight = 0.3f;       // คลิปที่ผสมอยู่น้อยกว่านี้ ไม่นับ event เท้า

    private SFXHandle _voice = SFXHandle.None;
    private SFXHandle _loop = SFXHandle.None;
    private SoundID _loopId;

    private HandSlamDust _slamSource;
    private Health _health;
    private AttackSphereCast _attack;
    private Health _playerHealth;
    private Transform _goreBone;
    private bool _goreBoneSearched;

    private Animator _animator;
    private int _chargeHash;
    private bool _charging;
    private SFXHandle _chargeHandle = SFXHandle.None;

    private int _stateHash;
    private float _stateEnterTime;
    private SFXHandle _stateLoop = SFXHandle.None;
    private PairedAnimationActor _pairedActor;
    private float _nextDamageHurt;
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

    /// <summary>หาเสียงของช่องนี้ — false = ผีตัวนี้ไม่มีเสียงช่องนี้</summary>
    public bool TryGetSlot(GhostSoundSlot slot, out SoundID sound, out float delay)
    {
        sound = null;
        delay = 0f;
        if (slot == GhostSoundSlot.None || _slots == null) return false;

        foreach (GhostSlotSound s in _slots)
        {
            if (s == null || s.Slot != slot || s.Sound == null) continue;
            sound = s.Sound;
            delay = s.Delay;
            return true;
        }
        return false;
    }

    /// <summary>ตายแล้ว — หยุดหายใจ / งึมงำ / กรีดร้องเป็นช่วง ๆ / เสียงวน (เสียงตายที่เพิ่งสั่งปล่อยให้จบเอง)</summary>
    public void StopIdleSounds()
    {
        StopRandomVoice();
        StopBreath();
        StopLoop(0.3f);
        StopChargeLoop(0.3f);
        StopStateLoop(0.3f);
        _breathOn = false;
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

    /// <summary>
    /// ใส่เป็น Animation Event ชื่อ GhostStep ตรงเฟรมเท้าแตะพื้นในท่าเดิน/วิ่ง
    /// ข้ามคลิปที่ผสมอยู่น้อย (blend tree / ตอนเปลี่ยนท่า) จะได้ไม่มีเสียงเท้าเกิน
    /// </summary>
    public void GhostStep(AnimationEvent e)
    {
        if (e != null && e.isFiredByAnimator && e.animatorClipInfo.weight < EventMinWeight) return;
        Footstep();
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
        _health = GetComponentInParent<Health>();
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInParent<Animator>();
        _chargeHash = string.IsNullOrEmpty(_chargeState) ? 0 : Animator.StringToHash(_chargeState);
        if (_stateSounds != null)
            foreach (GhostStateSound s in _stateSounds)
                if (s != null) s.Hash = string.IsNullOrEmpty(s.State) ? 0 : Animator.StringToHash(s.State);

        _pairedActor = GetComponentInParent<PairedAnimationActor>();

        _attack = GetComponent<AttackSphereCast>();
        if (_attack == null) _attack = GetComponentInParent<AttackSphereCast>();

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
        if (_health != null) _health.OnDead += StopIdleSounds;   // ตายทุกแบบ (รวมโดน Glory) — งึมงำ / หายใจหยุด
        if (_attack != null) _attack.OnHit += OnAttackHit;
        if (_health != null) _health.OnHurt += OnDamaged;
        _stateHash = 0;
        if (_stateSounds != null)
            foreach (GhostStateSound s in _stateSounds)
                if (s != null) { s.Pending = false; s.Played = false; }
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
        if (_health != null) _health.OnDead -= StopIdleSounds;
        if (_attack != null) _attack.OnHit -= OnAttackHit;
        if (_health != null) _health.OnHurt -= OnDamaged;

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
        _chargeHandle.Stop();
        _chargeHandle = SFXHandle.None;
        _charging = false;
        _stateLoop.Stop();
        _stateLoop = SFXHandle.None;
    }

    private void LateUpdate()
    {
        if (_autoSteps) UpdateAutoSteps();
        UpdateMove();
        UpdateRandomVoice();
        UpdateVoiceTail();
        UpdateBreath();
        UpdateCharge();
        UpdateStateSounds();
    }

    // ---------- เสียงตาม state ใน Animator ----------

    private void UpdateStateSounds()
    {
        if (_stateSounds == null || _stateSounds.Length == 0) return;
        if (_animator == null || !_animator.isActiveAndEnabled || SoundManager.Instance == null) return;
        if (_health != null && _health.IsDead) return;

        // กำลังเปลี่ยนท่า = นับ state ปลายทาง เสียงจะได้มาพร้อมท่าใหม่
        int hash = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0).shortNameHash
            : _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        if (hash != _stateHash)
        {
            foreach (GhostStateSound s in _stateSounds)
            {
                if (s == null || s.Hash == 0) continue;
                if (s.Hash == _stateHash)
                {
                    s.Pending = false;
                    if (s.Exit != null) SoundManager.Instance.PlaySFXAttached(s.Exit, transform);
                }
            }
            StopStateLoop(0.3f);

            _stateHash = hash;
            _stateEnterTime = Time.time;
            foreach (GhostStateSound s in _stateSounds)
            {
                if (s == null || s.Hash != hash) continue;
                s.Pending = s.Enter != null && !(s.OnlyFirstTime && s.Played);
                if (s.Loop != null && !_stateLoop.IsValid)
                {
                    _stateLoop = SoundManager.Instance.PlayLoopSFXForeverAttached(s.Loop, transform);
                    if (_stateLoop.IsValid)
                    {
                        _stateLoop.SetVolumeMultiplier(0f);
                        _stateLoop.FadeToVolumeMultiplier(1f, 0.3f);
                    }
                }
            }
        }

        foreach (GhostStateSound s in _stateSounds)
        {
            if (s == null || !s.Pending || s.Hash != _stateHash) continue;
            if (Time.time - _stateEnterTime < s.Delay) continue;
            s.Pending = false;
            s.Played = true;
            SoundManager.Instance.PlaySFXAttached(s.Enter, transform);
            Log($"state {s.State} → {s.Enter.name}");
        }
    }

    private void StopStateLoop(float fadeTime)
    {
        if (!_stateLoop.IsValid) return;
        _stateLoop.FadeOutAndStop(fadeTime);
        _stateLoop = SFXHandle.None;
    }

    // ---------- ร้องเจ็บตอนโดนดาเมจ ----------

    private void OnDamaged()
    {
        if (_damageHurt == null || SoundManager.Instance == null) return;
        if (_health != null && _health.IsDead) return;
        if (Time.time < _nextDamageHurt || InGlory()) return;

        _nextDamageHurt = Time.time + _damageHurtCooldown * UnityEngine.Random.Range(0.8f, 1.2f);
        SoundManager.Instance.PlaySFXAttached(_damageHurt, transform);
        Log($"เจ็บ {_damageHurt.name}");
    }

    // กำลังเล่นท่า Glory อยู่ — ไม่ร้องทับเสียง Glory
    private bool InGlory()
    {
        if (_pairedActor == null) return false;
        PairedRequest request = _pairedActor.CurrentRequest;
        return request.Animation != null && request.Type == PairedActionType.Glory && !_pairedActor.IsFinished;
    }

    // ---------- พุ่งชน ----------

    private void UpdateCharge()
    {
        if (_chargeHash == 0 || _animator == null || !_animator.isActiveAndEnabled) return;

        bool now = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0).shortNameHash == _chargeHash
            : _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _chargeHash;
        if (now == _charging) return;
        _charging = now;
        if (SoundManager.Instance == null) return;

        if (now)
        {
            if (_chargeStart != null) SoundManager.Instance.PlaySFXAttached(_chargeStart, transform);
            if (_chargeLoop != null)
            {
                _chargeHandle.Stop();
                _chargeHandle = SoundManager.Instance.PlayLoopSFXForeverAttached(_chargeLoop, transform);
                if (_chargeHandle.IsValid)
                {
                    _chargeHandle.SetVolumeMultiplier(0f);
                    _chargeHandle.FadeToVolumeMultiplier(1f, 0.15f);
                }
            }
            Log("เริ่มพุ่ง");
        }
        else
        {
            StopChargeLoop(0.25f);
            if (_chargeStop != null && (_health == null || !_health.IsDead))
                SoundManager.Instance.PlaySFXAttached(_chargeStop, transform);
            Log("หยุดพุ่ง");
        }
    }

    private void StopChargeLoop(float fadeTime)
    {
        if (!_chargeHandle.IsValid) return;
        _chargeHandle.FadeOutAndStop(fadeTime);
        _chargeHandle = SFXHandle.None;
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

        // เสียงร้องอื่นยังเล่นอยู่ (เช่นเสียงโผล่) หรือกำลังโดน Glory — รออีกนิด ไม่ร้องทับ
        if (_voice.IsPlaying || InGlory())
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

    // ---------- ฟาด / ตีโดน ----------

    /// <summary>
    /// ชื่อตรงกับ Animation Event "CastAttack" ในท่าโจมตีโดยตั้งใจ — Unity เรียกทุก component บนตัวที่มีเมธอดชื่อนี้
    /// AttackSphereCast ทำดาเมจ ส่วนตัวนี้เล่นเสียงฟาดลม ไม่ต้องใส่ event เพิ่ม
    /// </summary>
    public void CastAttack() => PlaySwing();

    /// <summary>
    /// ชื่อตรงกับ Animation Event "ShootProjectile" (ท่าขว้างของกระสือ) — ProjectileShooter ยิง ส่วนตัวนี้เล่นเสียง
    /// </summary>
    public void ShootProjectile() => PlaySwing();

    /// <summary>ชื่อตรงกับ Animation Event "Shoot" (เช่น DeathBlossom ของตานี) — ProjectileShooter ยิง ส่วนตัวนี้เล่นเสียง</summary>
    public void Shoot() => PlaySwing();

    private void PlaySwing()
    {
        if (_swing == null || SoundManager.Instance == null) return;

        Transform origin = _attack != null ? _attack.AttackOrigin : null;
        Vector3 pos = origin != null ? origin.position : transform.position;
        SoundManager.Instance.PlaySFX(_swing, pos);
        Log($"ฟาด {_swing.name}");
    }

    /// <summary>Animation Event ชื่อ GhostGore — เสียงเลือด / เนื้อ ออกตรงคอ (เช่น กระสือล้วงคอ ควักเนื้อ)</summary>
    public void GhostGore()
    {
        if (_gore == null || SoundManager.Instance == null) return;

        if (!_goreBoneSearched)
        {
            _goreBoneSearched = true;
            Transform[] bones = FindBones("neck", "neck.x", "head", "head.x");
            if (bones.Length > 0) _goreBone = bones[0];
        }
        Vector3 pos = _goreBone != null ? _goreBone.position : transform.position + Vector3.up * 1.5f;
        SoundManager.Instance.PlaySFX(_gore, pos);
        Log($"เลือด {_gore.name}");
    }

    private void OnAttackHit()
    {
        SoundID hit = _charging && _chargeHit != null ? _chargeHit : _hit;   // กำลังพุ่ง = เสียงพุ่งชน
        if (hit == null || SoundManager.Instance == null) return;
        if (PlayerBlocked()) return;   // กัน / ปัดได้ — ไม่ใช่โดนเนื้อ

        Transform origin = _attack.AttackOrigin;
        Vector3 pos = origin != null ? origin.position : transform.position;
        SoundManager.Instance.PlaySFX(hit, pos);
        Log($"ตีโดน {hit.name}");
    }

    /// <summary>ผู้เล่นกำลังกัน/ปัด (BlockSystem เปิด IgnoreDamage) — ดาเมจไม่เข้า</summary>
    private bool PlayerBlocked()
    {
        if (_playerHealth == null)
        {
            BlockSystem block = FindAnyObjectByType<BlockSystem>();
            if (block != null)
            {
                _playerHealth = block.GetComponent<Health>();
                if (_playerHealth == null) _playerHealth = block.GetComponentInParent<Health>();
            }
        }
        return _playerHealth != null && _playerHealth.IgnoreDamage;
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
        {
            string n = t.name.ToLowerInvariant();
            int colon = n.LastIndexOf(':');   // rig Mixamo ชื่อกระดูกมี prefix เช่น mixamorig:LeftFoot
            if (colon >= 0) n = n.Substring(colon + 1);
            if (System.Array.IndexOf(names, n) >= 0) found.Add(t);
        }
        return found.ToArray();
    }

    private void Log(string message)
    {
        if (_logPlay) Debug.Log($"[GhostAudio] {name} {message}", this);
    }
}
