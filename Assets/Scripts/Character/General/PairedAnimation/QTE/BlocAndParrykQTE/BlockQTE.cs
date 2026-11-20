using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Yantra.UI;

// QTE กันผี (Tutorial จุดน้ำเงิน) — ผีง้างตี → เวลาช้าลง + ขึ้น "กด F ค้างไว้"
//  • ตีโดนตอนผู้เล่นกันอยู่ = ผ่าน (Blocked) เวลากลับปกติทันที
//  • ตีโดนตอนไม่ได้กัน = พลาด (Failed) โดนดาเมจ แต่เลือดไม่ลดต่ำกว่า Health Floor — ผีถอยแล้วตีใหม่ได้
//
// วางบน GameObject ลูกของผีตัวที่ใช้สอน (ตัวเดียวกับที่มี StateMachineController หรือลูกของมัน)
//  • State ท่าตีของผี ใส่ Action "Start Block QTE"
//  • อ่านผลใน Transition ด้วย Condition "Block QTE" (Blocked / Failed)
//  • ปลดล็อก Block / ต่อย ใช้ FlagAddition ของทีม ต่อผ่าน On Started / On Blocked (แบบเดียวกับโซน punch)
//
// สคริปต์นี้ไม่ได้ฟังปุ่ม F เอง — กด F แล้ว State ของผู้เล่นเข้า Block → ExecuteBlock เปิด Health.IgnoreDamage
// ตอนผีตีโดน (Health.OnHit) เราดูค่านี้ว่ากันอยู่ไหม (Parry ก็นับว่าผ่าน)
public class BlockQTE : MonoBehaviour
{
    public enum Result { None, Blocked, Failed }

    [Header("จังหวะผีตี")]
    [Tooltip("ผีตีโดนวินาทีที่เท่าไหร่ (เวลาในเกม นับจากผีเข้าท่าตี) — วงนับถอยหลังจะหมดพอดีตอนนี้\n" +
             "ไม่รู้ก็ไม่เป็นไร: ผีตีโดนครั้งแรกเกมจะวัดให้ แล้วรอบต่อไปใช้ค่าที่วัดได้เอง + บอกเลขใน Console ให้ก๊อปมาใส่")]
    [SerializeField, Min(0.05f)] private float _hitTime = 1.1f;

    [Tooltip("วงนับถอยหลังนานกี่วินาที (เวลาจริง) — เกมคำนวณเองว่าต้องเริ่มสโลว์ตอนไหนวงถึงจะหมดพอดีตอนผีตีโดน")]
    [SerializeField, Min(0.3f)] private float _ringSeconds = 2.5f;

    [Tooltip("วงหมดแล้ว ผียังไม่ตีโดนอีกกี่วินาที (เวลาในเกม) = ถือว่าตีวืด เลิก QTE รอผีตีรอบหน้า\n" +
             "ระหว่างนี้เวลากลับปกติ ไม่ลากสโลว์ค้าง")]
    [SerializeField, Min(0.1f)] private float _missAfter = 1f;

    [Header("สโลว์")]

    [Tooltip("ช้าเหลือกี่เท่า — 0.1 = ช้าลง 10 เท่า")]
    [SerializeField, Range(0.02f, 1f)] private float _slowScale = 0.1f;

    [Tooltip("จากปกติไปถึงช้าสุดใช้กี่วินาที (เวลาจริง)")]
    [SerializeField, Min(0f)] private float _slowInTime = 0.15f;

    [Tooltip("กด F ค้างแล้ว เวลาเร่งกลับเป็นเท่านี้ — ผีตีโดนไว ไม่ต้องค้างรอนาน / 1 = กลับปกติ\nปล่อยปุ่มก่อนโดน = ช้าลงอีกรอบ กดใหม่ทัน")]
    [SerializeField, Range(0.02f, 1f)] private float _holdTimeScale = 1f;

    [Tooltip("เร่งกลับใช้เวลากี่วินาที (เวลาจริง)")]
    [SerializeField, Min(0f)] private float _holdRecoverTime = 0.2f;

    [Tooltip("กันเหนียว: QTE ค้างนานเกินกี่วินาที (เวลาจริง) = เลิก คืนเวลาปกติ")]
    [SerializeField, Min(0.5f)] private float _giveUpAfter = 8f;

    [Tooltip("เริ่มสโลว์แล้วล้างซับที่ค้างอยู่ — ระบบซับนับเวลาจริง ไม่ช้าตามเกม บรรทัดถัดไปจะเด้งขึ้นกลางสโลว์ดูแปลก")]
    [SerializeField] private bool _clearSubtitleOnSlow = true;

    [Tooltip("ปรับ Physics ให้ช้าตามด้วย ของที่ตกหล่น / ragdoll จะไม่กระตุกตอนสโลว์")]
    [SerializeField] private bool _smoothPhysics = true;

    [Header("ระหว่าง QTE")]
    [Tooltip("ห้ามเดิน — กันเดินหนีออกนอกระยะตี (กด F ยกการ์ดยังได้)")]
    [SerializeField] private bool _lockMovement = true;

    [Tooltip("กล้องหันไปมองผีเอง เมาส์หันไม่ได้จนจบ QTE — ผู้เล่นเห็นผีง้างตีชัด ๆ")]
    [SerializeField] private bool _lookAtGhost = true;

    [Tooltip("จุดที่กล้องมอง — ว่าง = กระดูก Look Bone ของผีตัวที่ตี (ไม่ใช่ Humanoid = ตัวผี + Look Offset)\nถ้าลากอะไรใส่ช่องนี้ จะบวก Look Offset ด้วย")]
    [SerializeField] private Transform _lookTarget;
    [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("มองกระดูกไหนของผี — คนฝึกต่อสู้จ้องช่วงคอ/อกคู่ต่อสู้ เห็นทั้งหน้าทั้งมือ\nNeck = คอ / UpperChest = อก / Head = หน้า")]
    [SerializeField] private HumanBodyBones _lookBone = HumanBodyBones.Neck;

    [Tooltip("เลื่อนจุดมองจากกระดูก (เมตร) — Y ติดลบ = มองต่ำลง")]
    [SerializeField] private Vector3 _boneLookOffset = Vector3.zero;

    [Tooltip("ใช้เวลาหันกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _lookTurnTime = 0.15f;

    [Header("ผู้เล่น")]
    [SerializeField] private string _playerTag = "Player";

    [Tooltip("ระหว่าง QTE เลือดลดได้ต่ำสุดกี่ % — กันตายตอนกำลังสอน / 0 = ไม่กัน")]
    [SerializeField, Range(0f, 100f)] private float _healthFloorPercent = 25f;

    [Header("ครั้งเดียว")]
    [Tooltip("กันได้ครั้งแรกแล้วไม่ขึ้น QTE อีก — ผีตีต่อได้ตามปกติ (ช่วงสอนต่อย)")]
    [SerializeField] private bool _stopAfterBlocked = true;

    [Header("บทพูด (ว่างได้)")]
    [Tooltip("เล่นตอนกันได้ เช่น \"โอ้ย เป็นบ้าอะไรคะเนี่ย!?\"")]
    [SerializeField] private SubtitleDataSO _blockedLine;

    [Tooltip("เล่นตอนพลาดโดนตี")]
    [SerializeField] private SubtitleDataSO _failedLine;

    [Header("หลังกันได้ครั้งแรก (ว่างได้)")]
    [Tooltip("ป้าย Tutorial ที่ขึ้นหลังบทพูด Blocked Line จบ — ขึ้นต่อกันตามลำดับ เช่น Tut_Guard → Tut_Punch\nเคยเห็นแล้วข้าม")]
    [SerializeField] private TutorialDataSO[] _blockedTutorials;

    [Tooltip("กันได้แล้วรออย่างน้อยกี่วินาทีค่อยขึ้นป้าย — ให้ผู้เล่นเห็นเส้น Guard ลดก่อน")]
    [SerializeField, Min(0f)] private float _tutorialDelay = 1f;

    // ส่งตัวผู้เล่นมาด้วย — ต่อกับ FlagAddition.HandleEndInteract ได้เลย แบบเดียวกับ Trigger Zone Events
    // (เลือกอันที่อยู่หัวข้อ Dynamic GameObject)
    [Header("Events (ส่งตัวผู้เล่นมาด้วย)")]
    [Tooltip("ผีเริ่มตี QTE ขึ้น — ต่อ FlagAddition ปลดล็อก Block ตรงนี้")]
    public UnityEvent<GameObject> OnStarted;

    [Tooltip("กันได้ — ต่อ FlagAddition ปลดล็อกต่อยตรงนี้")]
    public UnityEvent<GameObject> OnBlocked;

    public UnityEvent<GameObject> OnFailed;

    [Header("ทดสอบ (คลิกขวาที่หัว Component) — ผีตีจำลองตอน Hit Time")]
    [SerializeField, Min(0f)] private float _testDamage = 1f;

    // ให้ UI ฟัง — โชว์ "กด F ค้างไว้"
    public static event Action<BlockQTE> AnyStarted;
    public static event Action<BlockQTE> AnyEnded;

    /// <summary>กันได้ไปแล้ว (QTE ไม่ขึ้นอีก) แต่ผีเข้าท่าตบรอบใหม่</summary>
    public static event Action<BlockQTE> AnyAttackAfterBlocked;

    public bool IsRunning => _running;

    /// <summary>ช่วงที่เวลาช้าแล้ว (ใช้โชว์ป้ายกดปุ่ม)</summary>
    public bool IsSlowing => _running && _elapsedGame >= SlowStart;

    /// <summary>ผีตีโดนวินาทีที่เท่าไหร่ (เวลาในเกม) — ใช้ค่าที่วัดได้จริงถ้าเคยโดนแล้ว</summary>
    public float HitTime => _measuredHitTime > 0f ? _measuredHitTime : _hitTime;

    // เริ่มสโลว์ตอนไหน วงถึงจะนับ Ring Seconds (เวลาจริง) แล้วหมดพอดีตอนผีตีโดน
    // ช่วงกำลังช้าลง เวลาในเกมยังเดินอยู่ราวครึ่งๆ ต้องหักออกด้วย
    private float SlowStart
    {
        get
        {
            float rampGame = _slowInTime * (1f + _slowScale) * 0.5f;
            return Mathf.Max(0f, HitTime - _ringSeconds * _slowScale - rampGame);
        }
    }

    /// <summary>ผู้เล่นกำลังกันอยู่ (กด F ค้าง)</summary>
    public bool IsHolding => _running && _health != null && _health.IgnoreDamage;

    /// <summary>เวลาที่เหลือก่อนโดนตี 1 → 0 (ใช้ทำวงนับถอยหลัง)
    /// เริ่มลดหลังเวลาช้าสุดแล้ว — ช่วงกำลังช้าลง เวลาในเกมยังเดินเร็ว ถ้านับตั้งแต่ตอนนั้นวงจะหายไปเยอะก่อนผู้เล่นทันเห็น</summary>
    public float Remaining01
    {
        get
        {
            if (!_running || _elapsedGame >= HitTime)
                return 0f;
            if (_slowFullAt < 0f)
                return 1f;
            float window = Mathf.Max(0.01f, HitTime - _slowFullAt);
            return Mathf.Clamp01(1f - (_elapsedGame - _slowFullAt) / window);
        }
    }

    public Result LastResult => _result;

    /// <summary>กันได้แล้ว กำลังรอบทพูดจบเพื่อส่งหน้าสอน</summary>
    public bool IsShowingTutorials { get; private set; }

    /// <summary>คัตซีนสั่งหน้าสอนเอง — กันได้แล้วยังไม่ขึ้น เก็บไว้จนคัตซีนเรียก ShowHeldTutorials()
    /// (หน้าสอนหยุดเกม ถ้าขึ้นกลางคัตซีน รินจะค้างกลางทางถอย)</summary>
    public bool DeferTutorials { get; set; }

    public bool HasBlockedLine => _blockedLine != null;

    private bool _tutorialsHeld;
    public bool HasEverBlocked => _hasBlocked;

    private Health _health;
    private GameObject _rootPlayer;
    private bool _running;
    private bool _hasBlocked;
    private Result _result;
    private float _elapsedGame;
    private float _elapsedReal;
    private float _scale = 1f;
    private float _slowFullAt = -1f;   // เวลาในเกมตอนช้าสุดแล้ว (-1 = ยังไม่ถึง)
    private float _measuredHitTime = -1f;
    private bool _subtitleCleared;   // ผีตีโดนจริงวินาทีที่เท่าไหร่ (วัดตอนเล่น / -1 = ยังไม่เคยโดน)

    private bool _ownsTime;
    private float _baseFixedDelta;

    private PlayerLocomotion _locomotion;
    private bool _movementLocked;
    private PlayerCameraController _camera;
    private bool _looking;

    private Transform _attacker;

    private bool _floorChanged;
    private float _savedFloor;
    private bool _restoreFloorPending;

    // ===== API =====

    /// <summary>หา BlockQTE ของผีตัวนี้: ใต้ตัว → ตัวแม่ → "ข้างๆ" (ลูกตรงๆ ของตัวแม่ชั้นไหนก็ได้)
    /// เช่น PKa(Enemy) (6) > EnemeyController (ตัวที่มี State Machine) + BlockQTE วางคู่กัน
    /// ดูแค่ลูกตรงๆ ของแต่ละชั้น → ผีตัวอื่นในกลุ่มเดียวกันไม่ไปเจอ BlockQTE ของผีตัวสอน</summary>
    public static BlockQTE FindFor(Transform ghost)
    {
        if (ghost == null)
            return null;

        BlockQTE qte = ghost.GetComponentInChildren<BlockQTE>(true);
        if (qte != null)
            return qte;
        qte = ghost.GetComponentInParent<BlockQTE>(true);
        if (qte != null)
            return qte;

        for (Transform parent = ghost.parent; parent != null; parent = parent.parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                qte = parent.GetChild(i).GetComponent<BlockQTE>();
                if (qte != null)
                    return qte;
            }
        }
        return null;
    }

    /// <summary>State ท่าตีของผีเรียกตอนเข้า — คืน true ถ้า QTE ทำงานอยู่</summary>
    public bool Begin(GameObject rootPlayer)
    {
        return Begin(rootPlayer, null);
    }

    /// <summary>attacker = ตัวผีที่ตีจริง (ตัวที่ขยับ) ให้กล้องหันไปหา</summary>
    public bool Begin(GameObject rootPlayer, Transform attacker)
    {
        if (_running)
            return true;
        // ล้างผลเก่าทุกครั้งที่ผีเริ่มตีใหม่ — Condition "Blocked" จะได้ไม่ค้าง true วนเข้า State เซซ้ำ
        _result = Result.None;
        if (!isActiveAndEnabled)
            return false;
        if (_stopAfterBlocked && _hasBlocked)
        {
            // กันได้ไปแล้ว ไม่ขึ้น QTE — แต่บอกคัตซีนว่าผีเริ่มตบอีกรอบ (ผู้เล่นยังขยับไม่ได้อยู่หรือเปล่า)
            AnyAttackAfterBlocked?.Invoke(this);
            return false;
        }

        if (rootPlayer == null)
            rootPlayer = FindPlayerRoot();

        _health = FindPlayerHealth(rootPlayer);
        if (_health == null)
        {
            Debug.LogWarning($"[BlockQTE] {name} หา Health ของผู้เล่นไม่เจอ", this);
            return false;
        }
        if (_health.IsDead)
            return false;

        _rootPlayer = rootPlayer;
        _attacker = attacker;
        RestoreHealthFloor();   // เผื่อรอบก่อนยังค้างอยู่
        ApplyHealthFloor();
        HoldPlayer(rootPlayer);

        _running = true;
        _result = Result.None;
        _elapsedGame = 0f;
        _elapsedReal = 0f;
        _scale = 1f;
        _slowFullAt = -1f;
        _subtitleCleared = false;
        _health.OnHit += HandlePlayerHit;

        OnStarted?.Invoke(rootPlayer);
        AnyStarted?.Invoke(this);
        return true;
    }

    /// <summary>เลิกกลางคัน ไม่มีผล (เช่น ผีโดนต่อยหลุดท่า / ออกจาก State ท่าตีก่อนตีโดน)</summary>
    public void Cancel()
    {
        if (!_running)
            return;

        Unsubscribe();
        RestoreTime();
        RestoreHealthFloor();
        End();
    }

    // ===== โดนตี =====

    // Health.OnHit ยิงก่อนตัดเลือด — ตอนนี้ยังอยู่ใน TakeDamage อย่าเพิ่งคืน Health Floor (คืนเฟรมหน้า)
    private void HandlePlayerHit()
    {
        if (!_running)
            return;

        bool blocked = _health != null && _health.IgnoreDamage;
        _result = blocked ? Result.Blocked : Result.Failed;
        LearnHitTime(_elapsedGame);
        if (blocked)
            _hasBlocked = true;

        Unsubscribe();
        RestoreTime();
        _restoreFloorPending = true;
        End();

        if (blocked)
        {
            PlayLine(_blockedLine);
            if (_blockedTutorials != null && _blockedTutorials.Length > 0)
            {
                if (DeferTutorials)
                {
                    _tutorialsHeld = true;
                }
                else
                {
                    IsShowingTutorials = true;
                    StartCoroutine(ShowTutorialsAfterLine());
                }
            }
            OnBlocked?.Invoke(_rootPlayer);
        }
        else
        {
            PlayLine(_failedLine);
            OnFailed?.Invoke(_rootPlayer);
        }
    }

    // วัดว่าผีตีโดนจริงวินาทีที่เท่าไหร่ — รอบหน้าวงจะหมดตรงจังหวะเอง / บอกเลขให้ไปใส่ช่อง Hit Time ถาวร
    private void LearnHitTime(float measured)
    {
        if (measured <= 0.05f)
            return;

        float before = HitTime;
        _measuredHitTime = measured;
        if (Mathf.Abs(measured - _hitTime) > 0.05f)
            Debug.Log($"[BlockQTE] ผีตีโดนจริงที่ {measured:0.00} วิ (ช่อง Hit Time ตั้งไว้ {_hitTime:0.00}) — รอบต่อไปใช้ {measured:0.00} ให้เอง / " +
                      $"อยากให้ถาวร: หยุดเกมแล้วใส่ Hit Time = {measured:0.00}" + (Mathf.Abs(before - measured) > 0.05f ? "" : " (ตรงกับที่วัดรอบก่อน)"), this);
    }

    // ===== เวลา =====

    private void Update()
    {
        if (_restoreFloorPending)
        {
            _restoreFloorPending = false;
            RestoreHealthFloor();
        }

        if (!_running)
            return;

        if (_health == null || _health.IsDead)
        {
            Cancel();
            return;
        }

        // Pause / เมนู / HitStop คุมเวลาอยู่ — รอไปก่อน ไม่นับเวลาด้วย
        if (TimeOwnedByOthers())
            return;

        _elapsedReal += Time.unscaledDeltaTime;
        if (_elapsedReal >= _giveUpAfter)
        {
            Debug.LogWarning($"[BlockQTE] {name} QTE ค้างนานเกิน {_giveUpAfter} วิ — เลิก QTE (เช็คว่าท่าตีของผีโดนผู้เล่นจริง)", this);
            Cancel();
            return;
        }

        _elapsedGame += Time.deltaTime;

        float hitTime = HitTime;
        if (_elapsedGame >= hitTime + _missAfter)
        {
            Debug.Log($"[BlockQTE] {name} ผีตีวืด (เลย Hit Time {hitTime:0.00} วิ มา {_missAfter:0.0} วิแล้วยังไม่โดน) — รอผีตีรอบหน้า", this);
            Cancel();
            return;
        }

        // ถึงช่วงสโลว์แล้ว: กดค้าง = เร่งกลับ (กันอยู่แล้ว รอแค่หมัดถึง) / ไม่กด = ช้า
        // วงหมดแล้ว (เลย Hit Time) = เวลากลับปกติ ให้หมัดถึงเลย ไม่ลากสโลว์ค้างทั้งที่วงหมด
        bool slowPhase = _elapsedGame >= SlowStart;
        bool late = _elapsedGame >= hitTime;

        if (slowPhase && _clearSubtitleOnSlow && !_subtitleCleared)
        {
            _subtitleCleared = true;
            if (SubtitleSystem.Instance != null && SubtitleSystem.Instance.IsPlaying)
                SubtitleSystem.Instance.StopSubtitle();
        }
        bool holding = slowPhase && _health.IgnoreDamage;
        float target = !slowPhase || late ? 1f : holding ? _holdTimeScale : _slowScale;
        float rampTime = holding || late ? _holdRecoverTime : _slowInTime;
        _scale = rampTime > 0f
            ? Mathf.MoveTowards(_scale, target, (1f - _slowScale) * Time.unscaledDeltaTime / rampTime)
            : target;

        // เขียนทุกเฟรม — HitStop / Pause คืนเวลาเป็น 1 ทับเราได้ เฟรมถัดไปก็ช้ากลับเหมือนเดิม
        if (_scale < 0.999f || _ownsTime)
            SetTimeScale(_scale);

        if (_slowFullAt < 0f && slowPhase && (holding || _scale <= _slowScale + 0.001f))
            _slowFullAt = _elapsedGame;
    }

    private void SetTimeScale(float scale)
    {
        if (!_ownsTime)
        {
            _ownsTime = true;
            _baseFixedDelta = Time.fixedDeltaTime;
        }

        Time.timeScale = scale;
        if (_smoothPhysics)
            Time.fixedDeltaTime = _baseFixedDelta * scale;
    }

    private void RestoreTime()
    {
        if (!_ownsTime)
            return;
        _ownsTime = false;

        if (_smoothPhysics)
            Time.fixedDeltaTime = _baseFixedDelta;

        // Pause / Game Over / HitStop เป็นเจ้าของเวลาอยู่ ปล่อยให้เขาคืนเอง ไม่งั้นเกมหลุด Pause
        if (!TimeOwnedByOthers())
            Time.timeScale = 1f;
    }

    private static bool TimeOwnedByOthers()
    {
        if (Time.timeScale <= 0f) return true;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return true;
        if (UIManager.Instance != null && UIManager.Instance.HasAnyOpen) return true;
        return false;
    }

    // ===== ผู้เล่น =====

    private GameObject FindPlayerRoot()
    {
        GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
        return player != null ? player.transform.root.gameObject : null;
    }

    // Health ตัวเดียวกับที่ BlockSystem ใช้ (ExecuteBlock เปิด IgnoreDamage ที่ตัวนี้)
    private static Health FindPlayerHealth(GameObject rootPlayer)
    {
        if (rootPlayer == null)
            return null;

        BlockSystem block = rootPlayer.GetComponentInChildren<BlockSystem>(true);
        if (block != null && block.TryGetComponent(out Health blockHealth))
            return blockHealth;

        return rootPlayer.GetComponentInChildren<Health>(true);
    }

    private void ApplyHealthFloor()
    {
        if (_healthFloorPercent <= 0f || _health == null)
            return;

        _savedFloor = _health.MinHealthPercent;
        if (_savedFloor >= _healthFloorPercent)
            return;

        _health.SetMinHealthPercent(_healthFloorPercent);
        _floorChanged = true;
    }

    private void RestoreHealthFloor()
    {
        if (!_floorChanged)
            return;
        _floorChanged = false;

        // มีระบบอื่นตั้งค่าใหม่ไปแล้วระหว่างนั้น (SetHealthFloor ของ State) — ไม่ทับ
        if (_health != null && Mathf.Approximately(_health.MinHealthPercent, _healthFloorPercent))
            _health.SetMinHealthPercent(_savedFloor);
    }

    // ===== อื่น ๆ =====

    // รอบทพูดตอนกันได้จบก่อน — ป้าย Tutorial เปิดแล้วซับจะถูกซ่อนค้างไว้ ผู้เล่นพลาดประโยค
    /// <summary>คัตซีนจบแล้ว ขึ้นหน้าสอนที่เก็บไว้ — คืน true ถ้ามีหน้าสอนขึ้นจริง</summary>
    public bool ShowHeldTutorials()
    {
        if (!_tutorialsHeld)
            return false;
        _tutorialsHeld = false;

        bool shown = false;
        foreach (TutorialDataSO tutorial in _blockedTutorials)
            if (tutorial != null && !TutorialScreen.HasSeen(tutorial))
                shown |= TutorialScreen.TryShow(tutorial);
        return shown;
    }

    private IEnumerator ShowTutorialsAfterLine()
    {
        yield return new WaitForSecondsRealtime(_tutorialDelay);

        float waited = 0f;
        while (SubtitleSystem.Instance != null && SubtitleSystem.Instance.IsPlaying && waited < 15f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // เรียกต่อกันได้เลย หน้า Tutorial ต่อคิวให้เอง ปิดอันแรกแล้วอันถัดไปขึ้นต่อ
        if (_blockedTutorials != null)
        {
            foreach (TutorialDataSO tutorial in _blockedTutorials)
                if (tutorial != null && !TutorialScreen.HasSeen(tutorial))
                    TutorialScreen.TryShow(tutorial);
        }
        IsShowingTutorials = false;
    }


    private static void PlayLine(SubtitleDataSO line)
    {
        if (line != null && SubtitleSystem.Instance != null)
            SubtitleSystem.Instance.PlaySubtitle(line);
    }

    private void Unsubscribe()
    {
        if (_health != null)
            _health.OnHit -= HandlePlayerHit;
    }

    private void End()
    {
        _running = false;
        ReleasePlayer();
        AnyEnded?.Invoke(this);
    }

    // ===== ล็อกเดิน + หันกล้องหาผี =====

    private void HoldPlayer(GameObject rootPlayer)
    {
        if (_lockMovement && rootPlayer != null)
        {
            _locomotion = rootPlayer.GetComponentInChildren<PlayerLocomotion>(true);
            if (_locomotion != null)
            {
                _locomotion.LockLocomotion(this);
                _movementLocked = true;
            }
        }

        if (!_lookAtGhost)
            return;

        _camera = rootPlayer != null ? rootPlayer.GetComponentInChildren<PlayerCameraController>(true) : null;
        if (_camera == null)
            _camera = FindFirstObjectByType<PlayerCameraController>();

        Transform target = LookTarget(out Vector3 offset);
        // ระยะเวลานับด้วยเวลาในเกม (โดนสโลว์) — ตั้งยาวไว้ แล้วสั่งหยุดเองตอนจบ QTE
        if (_camera != null && target != null)
            _looking = _camera.StartForcedLook(target, offset, 999f, _lookTurnTime);
    }

    private void ReleasePlayer()
    {
        if (_movementLocked && _locomotion != null)
            _locomotion.UnlockLocomotion(this);
        _movementLocked = false;

        if (_looking && _camera != null)
            _camera.StopForcedLook();
        _looking = false;
    }

    private Transform LookTarget(out Vector3 offset)
    {
        offset = _lookOffset;
        if (_lookTarget != null)
            return _lookTarget;

        if (_attacker != null)
        {
            // ตัวผีที่ตีจริงยืนที่เท้า + 1.5 ม. = สูงกว่าหัวผีตอนก้มตัวตี กล้องเลยเงย → มองกระดูกคอ/อกแทน
            Animator animator = _attacker.GetComponentInChildren<Animator>();
            Transform bone = animator != null && animator.isHuman ? animator.GetBoneTransform(_lookBone) : null;
            if (bone != null)
            {
                offset = _boneLookOffset;
                return bone;
            }
            return _attacker;
        }

        Health ghost = GetComponentInParent<Health>();
        if (ghost != null)
            return ghost.transform;
        return transform.parent != null ? transform.parent : transform;
    }

    private void OnDisable()
    {
        IsShowingTutorials = false;
        Cancel();
        _restoreFloorPending = false;
        RestoreHealthFloor();
    }

    // ===== ทดสอบ =====

    [ContextMenu("ทดสอบ: เริ่ม QTE + ผีตีจำลอง")]
    private void TestRun()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[BlockQTE] ต้องกด Play ก่อน", this);
            return;
        }

        bool wasBlocked = _hasBlocked;
        _hasBlocked = false;   // ทดสอบซ้ำได้เรื่อย ๆ

        if (!Begin(null))
        {
            _hasBlocked = wasBlocked;
            Debug.LogWarning("[BlockQTE] เริ่มไม่ได้ — ดูข้อความสีเหลืองอันอื่นใน Console", this);
            return;
        }

        StartCoroutine(TestHit());
    }

    private IEnumerator TestHit()
    {
        yield return new WaitForSeconds(HitTime);   // เวลาในเกม — โดนสโลว์ด้วย เหมือนผีตีจริง

        if (!_running || _health == null)
            yield break;

        _health.TakeDamage(_testDamage);
        Debug.Log($"[BlockQTE] ผลทดสอบ: {_result}", this);
    }
}