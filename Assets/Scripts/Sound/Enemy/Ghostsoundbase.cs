using UnityEngine;

/// <summary>สถานะเสียงพูดของผี — ใช้จับคู่กับประโยคที่จะพูด</summary>
public enum GhostVoiceState { Idle, Patrol, Search, Chase, Attack, Dead }

[System.Serializable]
public struct VoiceLineSet
{
    public GhostVoiceState state;

    [Tooltip("ยัดหลายเทคใน SoundData แล้วเปิด Use Random Clips + Random Chance 100")]
    public SoundID line;

    [Tooltip("โอกาสพูดจริงในแต่ละครั้งที่ถูกเรียก (%) — อย่าตั้ง 100 เพราะผีที่พูดทุกครั้งจะคาดเดาได้")]
    [Range(0f, 100f)] public float chance;

    [Tooltip("ห่างขั้นต่ำระหว่างการพูดประโยคนี้ (วินาที)" +
        "\nสำคัญมาก เพราะ Animation Event จะยิงทุกรอบที่ท่าวนซ้ำ")]
    public float minCooldown;
    public float maxCooldown;

    [Tooltip("พูดได้เฉพาะในช่วงหลังได้รับสัญญาณ Alert เท่านั้น" +
        "\nแก้ปัญหาคลิปอนิเมชันที่วนตลอดเวลา ทำให้ผีพูดแม้ตอนไม่ได้ทำกิจกรรมนั้น" +
        "\nเช่น ตานีลอยตลอดเวลา แต่ควรพูด 'มึงอยู่ไหน' เฉพาะตอนกำลังหาจริงๆ")]
    public bool onlyDuringAlertWindow;

    [Tooltip("พูดประโยคนี้แล้วปิดหน้าต่าง Alert ทันที" +
        "\nติ๊กกับประโยคที่แปลว่า 'เลิกหาแล้ว' เช่นตอนเจอตัวหรือตอนโจมตี")]
    public bool closesAlertWindow;

    [Tooltip("ตัดคิวประโยคที่กำลังพูดค้างอยู่ได้ไหม" +
        "\nเปิดเฉพาะเสียงที่พลาดไม่ได้ เช่นเสียงกรี๊ดตอนโจมตี" +
        "\nถ้าเปิดหมดทุกอัน ผีจะพูดขัดตัวเองตลอดเวลา")]
    public bool canInterrupt;

    [HideInInspector] public float nextAllowedTime;
}

/// <summary>
/// สิ่งที่ผีทุกตัวมีเหมือนกัน — เสียงพูด เสียงคราง gacha เสียงตาย
///
/// คลาสนี้แปะตรงๆ ไม่ได้ (abstract) ต้องใช้ผ่าน KaSoundController หรือ TaniSoundController
/// อะไรที่เฉพาะตัวใครก็ไปอยู่ในคลาสลูก เช่น ฝีเท้าอยู่ที่ผีกะ เสียงหึ่งอยู่ที่ตานี
/// </summary>
public abstract class GhostSoundBase : MonoBehaviour
{
    // ==========================================
    // Voice Profiles (สุ่มบุคลิกเสียงตอนเกิด)
    // ==========================================
    [System.Serializable]
    public struct VoiceProfile
    {
        public string profileName;
        public float dropWeight;
        public SoundID idleMoan;
        public SoundID attackScream;
        public SoundID death;
    }

    [Header("Voice Gacha")]
    [SerializeField] private VoiceProfile[] voiceProfiles;
    private VoiceProfile currentVoice;

    // ==========================================
    // Auto-Moan
    // ==========================================
    [Header("Auto-Moan")]
    [SerializeField] private bool useAutoMoan = true;

    [Tooltip("โอกาสที่จะครางในแต่ละรอบ (0-100) แนะนำ 30-50 สำหรับมอนสเตอร์ที่อยู่เป็นฝูง")]
    [Range(0f, 100f)]
    [SerializeField] private float moanChance = 40f;

    [SerializeField] private float minMoanDelay = 8f;
    [SerializeField] private float maxMoanDelay = 15f;

    private float nextMoanAllowedTime = 0f;

    // ==========================================
    // Voice Lines (เรียกผ่าน Animation Event)
    // ==========================================
    // เสียงพูดของผีไม่ใช่บรรยากาศ แต่เป็น "หน้าจอแสดงสถานะ AI" ให้ผู้เล่น
    // ผู้เล่นต้องแยกออกภายในครึ่งวินาทีว่า "มันหาอยู่" กับ "มันเจอเราแล้ว" ต่างกันยังไง
    [Header("Voice Lines")]
    [SerializeField] private VoiceLineSet[] voiceLines;

    [Header("Movement Line Auto-Pick")]
    [Tooltip("ถ้าท่า Search กับ Chase ใช้คลิปเดียวกัน Animation Event จะแยกไม่ออก" +
        "\nเปิดอันนี้เพื่อให้ระบบเดาจากระยะห่างแทน — ไกล=ยังหาอยู่ ใกล้=เจอแล้ว")]
    [SerializeField] private bool autoPickByDistance = true;

    [Tooltip("ใกล้กว่านี้ (เมตร) ถือว่าเป็น Chase")]
    [SerializeField] private float chaseDistanceThreshold = 8f;

    // ==========================================
    // ==========================================
    // Presence Loop — เสียงที่ดังตลอดเวลาที่ผีตัวนี้มีตัวตน
    // ==========================================
    // ตานี = เสียงหึ่งตอนลอย / ผีกะที่ยืนกินซาก = เสียงเคี้ยว
    // ตั้ง Max Distance ใน SoundData ให้สั้น ผู้เล่นจะได้ยินเฉพาะตอนเข้าใกล้
    [Header("Presence Loop (Optional)")]
    [Tooltip("เสียงลูปประจำตัวผี เว้นว่างได้ถ้าไม่ต้องการ" +
        "\nเช่น เสียงหึ่งตอนลอย หรือเสียงเคี้ยวของผีที่ยืนกินซาก")]
    [SerializeField] private SoundID presenceLoop;

    [Tooltip("เสียงลูปชั้นที่สอง เช่นผ้าพลิ้ว ผมเสียดสี ของติดตัวกระทบกัน" +
        "\nเว้นว่างได้ถ้าไม่ต้องการ" +
        "\n⚠️ ทุกช่องที่ใส่ = กิน 1 voice ตลอดเวลาที่ผีอยู่ในระยะ" +
        "\nUnity เล่นพร้อมกันได้จริงแค่ 32 เสียง ผีเยอะๆ จะกินหมดเร็ว")]
    [SerializeField] private SoundID foleyLoop;

    [SerializeField] private float presenceFadeIn = 1.5f;
    [SerializeField] private float presenceFadeOut = 1f;

    private SFXHandle presenceHandle = SFXHandle.None;
    private SFXHandle foleyHandle = SFXHandle.None;

    // ==========================================
    // Alert Sound — เสียงเตือนผู้เล่น
    // ==========================================
    [Header("Alert Sound")]
    [Tooltip("เสียงเตือนตอนผีเปลี่ยนสถานะ — ดังทุกครั้ง 100% ไม่มีสุ่ม ไม่มี cooldown" +
        "\n⚠️ ตั้ง Spatial Blend = 0 ใน SoundData เพื่อให้ได้ยินจากทุกที่ในแมพ" +
        "\nถ้าตั้งเป็น 3D แล้วผีวาร์ปไปไกล ผู้เล่นจะไม่ได้ยินเลย ซึ่งผิดจุดประสงค์")]
    [SerializeField] private SoundID alertSound;

    [Tooltip("หลังได้รับ Alert ให้ถือว่าผี 'กำลังหา' นานกี่วินาที" +
        "\nประโยคที่ติ๊ก Only During Alert Window ไว้ จะพูดได้เฉพาะในช่วงนี้" +
        "\nตั้งให้ยาวพอที่ผีจะหาเจอ แต่ไม่ยาวจนพูดตอนทำอย่างอื่นอยู่")]
    [SerializeField] private float alertWindowDuration = 15f;

    private float alertWindowUntil = -999f;

    /// <summary>ตอนนี้อยู่ในช่วงเวลาหลังได้รับ Alert หรือเปล่า</summary>
    public bool IsAlertWindowOpen => Time.time < alertWindowUntil;

    /// <summary>ปิดหน้าต่างทันที — เรียกได้จาก Animation Event หรือ Event Channel</summary>
    public void CloseAlertWindow()
    {
        alertWindowUntil = -999f;

        if (logVoiceLines) Debug.Log($"[Alert] {name} ปิดหน้าต่าง Alert", this);
    }

    /// <summary>
    /// เล่นเสียงเตือนทันที ไม่ผ่านด่านคัดกรองใดๆ
    /// เรียกได้จาก Event Channel หรือ Animation Event
    /// </summary>
    public void PlayAlert()
    {
        if (alertSound == null || SoundManager.Instance == null) return;

        // เปิดหน้าต่างเวลาให้ประโยคที่ผูกไว้กับ Alert พูดได้
        alertWindowUntil = Time.time + alertWindowDuration;

        // เกิดที่ตำแหน่งหูผู้ฟัง — การันตีว่าได้ยินแน่นอนไม่ว่าผีจะอยู่ไกลแค่ไหน
        SoundManager.Instance.PlayEventSFX(alertSound);

        if (logVoiceLines)
            Debug.Log($"[Alert] {name} เล่นเสียงเตือน", this);
    }

    [Header("Debug")]
    [Tooltip("เปิดเพื่อดูว่าทำไมประโยคถึงไม่ถูกพูด — ปิดเมื่อหาเจอแล้ว")]
    [SerializeField] private bool logVoiceLines = false;

    [Header("Action Sounds")]
    [SerializeField] private SoundID[] actionSounds;

    private float voiceBusyUntil = -999f;
    private SFXHandle currentVoiceHandle = SFXHandle.None;

    protected bool IsDead { get; private set; }
    public bool IsVoiceBusy => Time.time < voiceBusyUntil;

    // ==========================================
    // Lifecycle — คลาสลูก override ได้ แต่ต้องเรียก base เสมอ
    // ==========================================
    protected virtual void OnEnable()
    {
        IsDead = false;
        RollVoiceGacha();
        ResetMoanTimer(0f);
        StartPresenceLoop();
    }

    protected virtual void OnDisable()
    {
        // ต้องหยุดเสมอ — PlayLoopSFXForeverAttached ไม่คืนลำโพงเข้าพูลเอง
        // ถ้าลืม ผีหายไปแล้วแต่เสียงยังดังอยู่ และลำโพงตัวนั้นหายจากพูลถาวร
        presenceHandle.Stop();
        presenceHandle = SFXHandle.None;

        foleyHandle.Stop();
        foleyHandle = SFXHandle.None;
    }

    protected virtual void Start()
    {
        // OnEnable() อาจทำงานก่อน SoundManager.Awake()
        // Unity ไม่รับประกันลำดับ Awake/OnEnable ระหว่าง GameObject คนละตัว
        // Start() ทำงานหลัง Awake ของทุกตัวเสมอ จึงปลอดภัยแน่นอน
        StartPresenceLoop();
    }

    private void StartPresenceLoop()
    {
        if (SoundManager.Instance == null) return;   // ยังไม่พร้อม Start() จะมาลองใหม่

        if (!presenceHandle.IsValid)
            presenceHandle = StartLoop(presenceLoop);

        if (!foleyHandle.IsValid)
            foleyHandle = StartLoop(foleyLoop);
    }

    private SFXHandle StartLoop(SoundID id)
    {
        if (id == null) return SFXHandle.None;

        SFXHandle handle = SoundManager.Instance.PlayLoopSFXForeverAttached(id, transform);
        if (!handle.IsValid) return SFXHandle.None;

        handle.SetVolumeMultiplier(0f);
        handle.FadeToVolumeMultiplier(1f, presenceFadeIn);
        return handle;
    }

    /// <summary>หยุดเสียงลูปประจำตัวทั้งหมด (ทั้ง Presence และ Foley)</summary>
    protected void StopPresenceLoop(float fadeTime)
    {
        presenceHandle.FadeOutAndStop(fadeTime);
        presenceHandle = SFXHandle.None;

        foleyHandle.FadeOutAndStop(fadeTime);
        foleyHandle = SFXHandle.None;
    }

    protected float PresenceFadeOut => presenceFadeOut;

    protected virtual void Update()
    {
        if (!useAutoMoan || IsDead) return;

        // อย่าครางทับประโยคที่กำลังพูดอยู่
        if (IsVoiceBusy) return;

        if (Time.time < nextMoanAllowedTime) return;

        if (Random.Range(0f, 100f) <= moanChance)
            PlayIdleMoan();
        else
            ResetMoanTimer(0f);
    }

    // ==========================================
    // Animation Event API
    // ==========================================

    /// <summary>ใส่ในท่าเดิน/ลอยไปข้างหน้า — ระบบจะเลือก Search/Chase ให้เอง</summary>
    public void PlayMovementLine()
    {
        if (!autoPickByDistance)
        {
            TrySpeak(GhostVoiceState.Search);
            return;
        }

        float sqrDist = AudioListenerCache.SqrDistanceToListener(transform.position);
        bool isNear = sqrDist <= chaseDistanceThreshold * chaseDistanceThreshold;

        TrySpeak(isNear ? GhostVoiceState.Chase : GhostVoiceState.Search);
    }

    public void PlaySearchLine() => TrySpeak(GhostVoiceState.Search);
    public void PlayChaseLine() => TrySpeak(GhostVoiceState.Chase);

    /// <summary>ใส่ในท่าโจมตี ตรงเฟรมที่มือแตะตัวผู้เล่นพอดี</summary>
    public void PlayAttackLine() => TrySpeak(GhostVoiceState.Attack);

    /// <summary>
    /// ทางเข้าหลักสำหรับระบบภายนอก เช่น Event Channel ของทีม
    /// ผู้เรียกแค่บอกว่า 'ตอนนี้สถานะอะไร' ไม่ต้องรู้เรื่อง AudioSource หรือ SoundID เลย
    /// </summary>
    public void PlayVoiceLine(GhostVoiceState state) => TrySpeak(state);

    /// <summary>
    /// ผีที่ไม่มีฝีเท้า (เช่นผีลอย) จะเมินคำสั่งนี้เงียบๆ
    /// จำเป็นต้องมีที่คลาสแม่ เพราะผีหลายตัวใช้คลิปอนิเมชันร่วมกัน
    /// ถ้าคลิปมี Event เรียก PlayFootstep แล้วผีตัวนั้นไม่มีเมธอดนี้
    /// Unity จะรัว warning 'has no receiver' ทุกเฟรมที่ยิง Event
    /// </summary>
    public virtual void PlayFootstep() { }

    public void PlayActionSound(int index)
    {
        if (actionSounds == null || index < 0 || index >= actionSounds.Length) return;
        PlaySoundAttached(actionSounds[index]);
    }

    public void PlayIdleMoan()
    {
        if (Time.time < nextMoanAllowedTime) return;
        float duration = PlaySoundAttached(currentVoice.idleMoan);
        ResetMoanTimer(duration);
    }

    public void PlayAttackScream()
    {
        float duration = PlaySoundAttached(currentVoice.attackScream);
        ResetMoanTimer(duration);
    }

    public virtual void PlayDeathSound()
    {
        PlaySoundAttached(currentVoice.death);
        IsDead = true;

        // ค่อยๆ เฟดจะฟังดูเป็นการ 'สลาย' มากกว่าตัดฉับ
        StopPresenceLoop(presenceFadeOut);
    }

    // ==========================================
    // Voice Line Core
    // ==========================================
    private void TrySpeak(GhostVoiceState state)
    {
        if (IsDead) { LogSkip(state, "ผีตายแล้ว (IsDead)"); return; }

        int index = FindLineIndex(state);
        if (index < 0) { LogSkip(state, "ไม่มีแถวนี้ใน Voice Lines"); return; }
        if (voiceLines[index].line == null) { LogSkip(state, "ช่อง Line ว่าง"); return; }

        if (voiceLines[index].onlyDuringAlertWindow && !IsAlertWindowOpen)
        {
            LogSkip(state, "อยู่นอกช่วง Alert Window (ยังไม่ได้รับสัญญาณ หรือหมดเวลาแล้ว)");
            return;
        }

        // กฎเหล็ก: หนึ่งผีพูดได้ทีละประโยค
        // ยกเว้นเสียงที่ติ๊ก Can Interrupt ไว้ ซึ่งจะตัดคิวประโยคเก่าทิ้ง
        if (IsVoiceBusy && !voiceLines[index].canInterrupt)
        {
            LogSkip(state, $"กำลังพูดประโยคอื่นอยู่ อีก {voiceBusyUntil - Time.time:F1} วิถึงจบ (ติ๊ก Can Interrupt ถ้าอยากให้แทรกได้)");
            return;
        }

        if (Time.time < voiceLines[index].nextAllowedTime)
        {
            LogSkip(state, $"ติด cooldown อีก {voiceLines[index].nextAllowedTime - Time.time:F1} วิ");
            return;
        }

        // ตั้ง cooldown ก่อนเสมอ แม้รอบนี้จะสุ่มแล้วไม่พูด
        // ไม่งั้น Animation Event ที่ยิงถี่ๆ จะสุ่มรัวจนโอกาสพูดสูงกว่าที่ตั้งไว้มาก
        float min = Mathf.Max(0f, voiceLines[index].minCooldown);
        float max = Mathf.Max(min, voiceLines[index].maxCooldown);
        voiceLines[index].nextAllowedTime = Time.time + Random.Range(min, max);

        // ความเงียบคือเครื่องมือ — ผีที่บางครั้งไม่พูด ทำให้ผู้เล่นไม่กล้าไว้ใจความเงียบ
        if (Random.Range(0f, 100f) > voiceLines[index].chance)
        {
            LogSkip(state, $"สุ่มไม่ผ่าน (Chance {voiceLines[index].chance}%)");
            return;
        }

        // ตัดประโยคเก่าแบบเฟดสั้นๆ ไม่ตัดฉับ เพราะจะได้ยินเป็นเสียง 'ป๊อป'
        if (IsVoiceBusy) currentVoiceHandle.FadeOutAndStop(0.08f);

        float duration = 0f;

        if (SoundManager.Instance != null)
        {
            currentVoiceHandle = SoundManager.Instance.PlaySFXAttachedTracked(
                voiceLines[index].line, transform, out duration);
        }

        voiceBusyUntil = Time.time + duration;

        if (voiceLines[index].closesAlertWindow) CloseAlertWindow();

        if (logVoiceLines)
            Debug.Log($"[Voice] {name} พูด {state} ยาว {duration:F2} วิ", this);

        ResetMoanTimer(duration);
    }

    private void LogSkip(GhostVoiceState state, string reason)
    {
#if UNITY_EDITOR
        if (logVoiceLines)
            Debug.LogWarning($"[Voice] {name} ไม่พูด {state} — {reason}", this);
#endif
    }

    private int FindLineIndex(GhostVoiceState state)
    {
        if (voiceLines == null) return -1;

        for (int i = 0; i < voiceLines.Length; i++)
        {
            if (voiceLines[i].state == state) return i;
        }
        return -1;
    }

    // ==========================================
    // Helpers (คลาสลูกใช้ได้)
    // ==========================================
    protected float PlaySound(SoundID id, Vector3 position)
    {
        if (id == null || SoundManager.Instance == null) return 0f;
        return SoundManager.Instance.PlaySFX(id, position);
    }

    protected float PlaySoundAttached(SoundID id)
    {
        if (id == null || SoundManager.Instance == null) return 0f;
        return SoundManager.Instance.PlaySFXAttached(id, transform);
    }

    protected void ResetMoanTimer(float clipDuration)
    {
        nextMoanAllowedTime = Time.time + clipDuration + Random.Range(minMoanDelay, maxMoanDelay);
    }

    private void RollVoiceGacha()
    {
        if (voiceProfiles == null || voiceProfiles.Length == 0) return;

        float totalWeight = 0f;
        foreach (var profile in voiceProfiles)
            totalWeight += profile.dropWeight;

        float randomVal = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        foreach (var profile in voiceProfiles)
        {
            currentSum += profile.dropWeight;
            if (randomVal <= currentSum)
            {
                currentVoice = profile;
                return;
            }
        }
    }
}