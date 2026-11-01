using SDFcl.GamePlay.Interactable;
using UnityEngine;

// วางบนตัวเดียวกับ ButtonInteractor + GloryInteractAddition ของผี (เช่น TestButtonInteract)
// ไม่แตะ BaseInteractor — แค่ถาม CanInteract ว่าตอนนี้กด Glory ได้หรือยัง
// ButtonInteractor ต้องติ๊ก Hide Interact ไว้ ไม่งั้นระบบจะนับว่าผีพร้อมตลอดเวลา
[RequireComponent(typeof(IbaseInteractor), typeof(Iinteractor))]
public class GloryFeedbackAddition : MonoBehaviour
{
    [Header("ตอนผีพร้อมโดน Glory")]
    [Tooltip("ขอบเรืองแสงของผี — เว้นว่าง = หาจากตัวแม่ให้เอง")]
    [SerializeField] private GhostRimGlow _rimGlow;

    [Tooltip("เสียงเตือนตอนผีเพิ่งพร้อม ดังติดตัวผี\nตั้งเป็นเสียง 3D ผู้เล่นจะได้รู้ว่าตัวไหน — เว้นว่าง = ไม่มีเสียง")]
    [SerializeField] private SoundID _readySound;

    [Tooltip("กันเสียงเตือนดังรัวๆ ถ้าสถานะพร้อมกะพริบเปิดปิด (วินาที)")]
    [SerializeField, Min(0f)] private float _readySoundCooldown = 2f;

    [Header("รางวัลหลัง Glory สำเร็จ")]
    [Tooltip("ฮีลกี่ % ของเลือดเต็มผู้เล่น — 0 = ไม่ฮีล")]
    [SerializeField, Range(0f, 100f)] private float _healPercent = 15f;

    [Tooltip("เสียงตอนได้เลือด ดังที่หูผู้เล่น — เว้นว่าง = ไม่มีเสียง")]
    [SerializeField] private SoundID _healSound;

    [Header("ผี (เว้นว่างได้ จะหาจากตัวแม่ให้เอง)")]
    [SerializeField] private Health _victimHealth;

    [Header("Debug")]
    [SerializeField] private bool _logSteps;

    // ท่า Glory โดนยกเลิกกลางทาง (ผีไม่ตาย) รอเท่านี้แล้วเลิกรอฮีล
    private const float CancelGrace = 1.5f;

    private IbaseInteractor _baseInteractor;
    private Iinteractor _interactor;
    private GameObject _player;
    private float _findTimer = 999f;

    private bool _ready;
    private float _lastReadySoundTime = -999f;

    private Health _healTarget;
    private PairedAnimationPlayer _gloryPlayer;
    private bool _waitingForKill;
    private float _notPlayingTime;

    private void Awake()
    {
        _baseInteractor = GetComponent<IbaseInteractor>();
        _interactor = GetComponent<Iinteractor>();

        if (_rimGlow == null)
            _rimGlow = GetComponentInParent<GhostRimGlow>();
        if (_victimHealth == null)
            _victimHealth = GetComponentInParent<Health>();

        if (_victimHealth == null)
            Debug.LogWarning("[GloryFeedback] หา Health ของผีไม่เจอ — จะไม่รู้ว่าผีตาย เลยไม่ฮีลให้ (ลากใส่ช่อง Victim Health เอง)", this);
    }

    private void OnEnable()
    {
        _interactor.OnInteract += HandleInteract;
        if (_victimHealth != null)
            _victimHealth.OnDead += HandleVictimDead;
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        if (_victimHealth != null)
            _victimHealth.OnDead -= HandleVictimDead;

        SetReady(false);
        _waitingForKill = false;
    }

    private void Update()
    {
        SetReady(IsReady());

        if (_waitingForKill)
            WatchForCancel();
    }

    // ---------- พร้อมโดน Glory ----------

    private bool IsReady()
    {
        if (_victimHealth != null && _victimHealth.IsDead)
            return false;
        if (!FindPlayer())
            return false;

        // ระหว่างเล่นท่า GloryInteractAddition ตอบ false ให้เอง ขอบเลยกลับเป็นสีปกติตอนกำลังฆ่า
        return _baseInteractor.CanInteract(_player);
    }

    private bool FindPlayer()
    {
        if (_player != null)
            return true;

        // ลองหาใหม่ทุกครึ่งวินาที เผื่อผู้เล่นถูก spawn ทีหลัง
        _findTimer += Time.deltaTime;
        if (_findTimer < 0.5f)
            return false;
        _findTimer = 0f;

        Health playerHealth = PlayerLookup.Find<Health>();
        if (playerHealth != null)
            _player = playerHealth.gameObject;
        return _player != null;
    }

    private void SetReady(bool ready)
    {
        if (ready == _ready)
            return;
        _ready = ready;

        if (_rimGlow != null)
            _rimGlow.SetGloryReady(ready);

        if (_logSteps)
            Debug.Log($"[GloryFeedback] {name} {(ready ? "พร้อมโดน Glory" : "ไม่พร้อมแล้ว")}", this);

        if (ready)
            PlayReadySound();
    }

    private void PlayReadySound()
    {
        if (_readySound == null || SoundManager.Instance == null)
            return;
        if (Time.time - _lastReadySoundTime < _readySoundCooldown)
            return;

        _lastReadySoundTime = Time.time;
        SoundManager.Instance.PlaySFXAttached(_readySound, transform);
    }

    // ---------- ฮีลหลังฆ่าสำเร็จ ----------

    private void HandleInteract(GameObject rootplayer)
    {
        if (_healPercent <= 0f || rootplayer == null)
            return;

        _healTarget = rootplayer.GetComponentInChildren<Health>();
        _gloryPlayer = rootplayer.GetComponentInChildren<PairedAnimationPlayer>();
        _waitingForKill = _healTarget != null;
        _notPlayingTime = 0f;
    }

    // KillOnPairedFinish ฆ่าผีตอนท่าจบ — ผีตายระหว่างรอ = Glory สำเร็จ
    private void HandleVictimDead()
    {
        if (!_waitingForKill)
            return;

        _waitingForKill = false;
        GiveHeal();
    }

    private void WatchForCancel()
    {
        bool playing = _gloryPlayer != null && _gloryPlayer.IsPlaying;
        _notPlayingTime = playing ? 0f : _notPlayingTime + Time.deltaTime;

        if (_notPlayingTime < CancelGrace)
            return;

        _waitingForKill = false;
        if (_logSteps)
            Debug.Log($"[GloryFeedback] {name} ท่าจบแต่ผีไม่ตาย — ไม่ฮีล", this);
    }

    private void GiveHeal()
    {
        if (_healTarget == null || _healTarget.IsDead)
            return;

        // ใช้ SetCurrentHealth แทน Heal เพราะ Heal ไม่ทำงานตอนเปิด IgnoreDamage อยู่ (เช่นช่วงท่า Glory)
        float amount = _healTarget.MaxHealth * _healPercent / 100f;
        _healTarget.SetCurrentHealth(_healTarget.CurrentHP + amount);

        if (_healSound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlayEventSFX(_healSound);

        if (_logSteps)
            Debug.Log($"[GloryFeedback] Glory สำเร็จ ฮีล +{amount:0.#} → {_healTarget.CurrentHP:0.#}/{_healTarget.MaxHealth:0.#}", this);
    }
}
