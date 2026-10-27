using System;
using SDFcl.GamePlay.Interactable;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// ผีจับผู้เล่น → ผู้เล่นกด E รัวๆ ให้หลุด ระหว่างโดนจับเสียเลือดเรื่อยๆ
// วางคู่กับ Interactor บนลูกของผี (Hold Interact ปิด) แบบเดียวกับ GloryInteractAddition
// ผู้เล่นกด E ใส่ผีเพื่อเริ่มเองไม่ได้ — เริ่มได้จาก BeginQTE เท่านั้น (State ของผีเรียกผ่าน StartGrabQTEActionSO)
[RequireComponent(typeof(Iinteractor), typeof(IbaseInteractor))]
public class GrabQTEInteractAddition : MonoBehaviour, IPairedInteractionSource
{
    public enum Result { None, Escaped, Failed }

    [Header("ท่า")]
    [Tooltip("PairedAnimationActor ของผี — ตัวเดียวกับที่มี StateMachineController")]
    [SerializeField] private PairedAnimationActor _ghost;

    [Tooltip("ท่าโดนจับของผู้เล่น — ใน asset ต้องติ๊ก Loop")]
    [SerializeField] private PairedAnimationId _playerLoop;

    [Tooltip("ท่าจับของผี — ใน asset ต้องติ๊ก Loop")]
    [SerializeField] private PairedAnimationId _ghostLoop;

    [Tooltip("ท่าดิ้นหลุด — ว่างได้")]
    [SerializeField] private PairedAnimationId _playerEscape;
    [SerializeField] private PairedAnimationId _ghostEscape;

    [Tooltip("ท่าตอนแพ้ — ว่างได้")]
    [SerializeField] private PairedAnimationId _playerFail;
    [SerializeField] private PairedAnimationId _ghostFail;

    [Tooltip("ผู้เล่นยืนห่างผีเท่านี้ตอนโดนจับ (เมตร)")]
    [SerializeField, Min(0f)] private float _distance = 1f;

    [Header("กดรัว")]
    [Tooltip("ปุ่มเดียวกับปุ่ม Interact ของ PlayerInteract (E)")]
    [SerializeField] private InputActionReference _mashInput;

    [Tooltip("กด 1 ครั้งหลอดขึ้นเท่าไหร่ (หลอดเต็ม = 1)")]
    [SerializeField, Range(0.01f, 1f)] private float _progressPerPress = 0.08f;

    [Tooltip("ไม่กดแล้วหลอดลดต่อวินาที")]
    [SerializeField, Min(0f)] private float _decayPerSecond = 0.15f;

    [Tooltip("เวลาจำกัด (วินาที) หมดเวลา = แพ้ / 0 = ไม่จำกัด แพ้เมื่อเลือดหมดเท่านั้น")]
    [SerializeField, Min(0f)] private float _timeLimit = 0f;

    [Header("ดาเมจระหว่างโดนจับ")]
    [SerializeField, Min(0f)] private float _damagePerSecond = 1f;

    [Tooltip("ตัดเลือดทีละก้อนทุกกี่วินาที")]
    [SerializeField, Min(0.05f)] private float _damageInterval = 0.5f;

    [Tooltip("ไม่ติ๊ก = ตัดเลือดเงียบๆ ไม่ยิง OnHurt / OnHit (กันผู้เล่นโดนกระชากไป Stagger กลางท่า)\nติ๊ก = ใช้ TakeDamage ปกติ")]
    [SerializeField] private bool _useHurtEvents = false;

    [Header("ตอนแพ้")]
    [Tooltip("ติ๊ก = ตายทันที / ไม่ติ๊ก = โดนดาเมจก้อนข้างล่างแล้วหลุด")]
    [SerializeField] private bool _killOnFail = false;
    [SerializeField, Min(0f)] private float _failDamage = 3f;

    [Header("Events")]
    public UnityEvent OnStarted;
    public UnityEvent OnEscaped;
    public UnityEvent OnFailed;

    [Tooltip("ทุกครั้งที่กด E ระหว่างโดนจับ — ใส่เสียงดิ้น / จอสั่นเบาๆ")]
    public UnityEvent OnMash;

    [Tooltip("ทุกครั้งที่โดนตัดเลือด — ใส่เสียงเจ็บ / จอแดง")]
    public UnityEvent OnDamageTick;

    public event Action Mashed;

    // ให้ UI ฟัง — โชว์ "กด E รัวๆ" + หลอด
    public static event Action<GrabQTEInteractAddition> AnyStarted;
    public static event Action<GrabQTEInteractAddition> AnyEnded;

    public bool IsRunning => _running;
    public bool IsMashing => _running && !_resolved && _player != null && _player.IsLooping;
    public float Progress01 => _progress;
    public float TimeLeft => _timeLimit > 0f ? Mathf.Max(0f, _timeLimit - _elapsed) : -1f;
    public Result LastResult => _result;

    private Iinteractor _interactor;
    private IbaseInteractor _baseInteractor;
    private Health _ghostHealth;

    private GameObject _rootPlayer;
    private PairedAnimationPlayer _player;
    private Health _playerHealth;
    private InputAction _input;

    private bool _allowStart;
    private bool _running;
    private bool _resolved;
    private float _progress;
    private float _elapsed;
    private float _damageTimer;
    private Result _result;

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
        _baseInteractor = GetComponent<IbaseInteractor>();
    }

    private void Start()
    {
        if (_ghost == null)
            return;

        _ghostHealth = _ghost.GetComponentInParent<Health>();
        if (_ghostHealth == null)
            _ghostHealth = _ghost.GetComponentInChildren<Health>();
    }

    private void OnEnable()
    {
        _interactor.OnInteract += HandleInteract;
        _interactor.AddCanInteractModifier(CanStart);
        _interactor.AddCanCancelInteractModifier(CanCancel);
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        _interactor.RemoveCanInteractModifier(CanStart);
        _interactor.RemoveCanCancelInteractModifier(CanCancel);

        if (_running)
        {
            if (_ghost != null)
                _ghost.EndLoop(null);
            if (_player != null)
                _player.EndLoop(null);
            Cleanup();
        }
    }

    // ===== API =====

    // State ของผีเรียกตอนจับ — คืน true ถ้าเริ่มได้
    public bool BeginQTE(GameObject rootPlayer)
    {
        if (_running || rootPlayer == null)
            return false;

        if (_ghost == null || _playerLoop == null || _ghostLoop == null)
        {
            Debug.LogWarning($"[GrabQTE] {name} ยังใส่ Ghost / Player Loop / Ghost Loop ไม่ครบ", this);
            return false;
        }

        if (_ghostHealth != null && _ghostHealth.IsDead)
            return false;

        // เปิดทางให้ Interact ผ่านแค่ตอนนี้ ผ่าน Interactor ปกติ ผู้เล่นจะเข้า PlayerInteract_State เอง
        _allowStart = true;
        _baseInteractor.Interact(rootPlayer, true);
        _allowStart = false;

        return _running;
    }

    // ===== Interactor =====

    // ผู้เล่นกด E ใส่ผีเพื่อเริ่มเองไม่ได้ ไฟ highlight ก็จะไม่ขึ้น
    private bool CanStart(GameObject rootPlayer) => _allowStart;

    // ระหว่างโดนจับ ยกเลิกเองไม่ได้ — ท่าจบแล้ว PairedAnimationPlayer ยกเลิกด้วย force จึงไม่ติดตัวนี้
    private bool CanCancel(GameObject rootPlayer) => !_running;

    private void HandleInteract(GameObject rootPlayer)
    {
        if (!_allowStart)
            return;

        PairedAnimationPlayer player = rootPlayer.GetComponentInChildren<PairedAnimationPlayer>();
        if (player == null)
        {
            Debug.LogWarning($"[GrabQTE] {name} หา PairedAnimationPlayer บนผู้เล่นไม่เจอ", this);
            return;
        }

        // ผีหันหาผู้เล่น ผู้เล่นยืนตรงหน้าผีแล้วหันเข้าหา — สูตรเดียวกับ Glory
        Vector3 ghostPosition = _ghost.transform.position;
        Vector3 toPlayer = player.transform.position - ghostPosition;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = Vector3.ProjectOnPlane(_ghost.transform.forward, Vector3.up);
        toPlayer.Normalize();

        Quaternion ghostRotation = Quaternion.LookRotation(toPlayer);
        Vector3 playerPosition = ghostPosition + toPlayer * _distance;
        playerPosition.y = player.transform.position.y;
        Quaternion playerRotation = Quaternion.LookRotation(-toPlayer);

        PairedRequest request = PairedRequest
            .InPlace(PairedActionType.Grab, _playerLoop, playerPosition, playerRotation, _baseInteractor, rootPlayer)
            .WithPartner(_ghost, _ghostLoop, ghostPosition, ghostRotation);

        // เริ่มไม่ได้ PairedAnimationPlayer จะยกเลิก Interact ให้เองในเฟรมถัดไป
        if (!player.Play(request))
            return;

        _rootPlayer = rootPlayer;
        _player = player;
        _running = true;
        _resolved = false;
        _result = Result.None;
        _progress = 0f;
        _elapsed = 0f;
        _damageTimer = 0f;

        _playerHealth = rootPlayer.GetComponentInChildren<Health>();
        if (_playerHealth != null)
            _playerHealth.OnDead += HandlePlayerDead;
        if (_ghostHealth != null)
            _ghostHealth.OnDead += HandleGhostDead;

        if (_mashInput != null && _mashInput.action != null)
        {
            _input = _mashInput.action;
            _input.started += HandleMash;
        }
        else
        {
            Debug.LogWarning($"[GrabQTE] {name} ยังไม่ได้ใส่ Mash Input — กดปุ่มแล้วจะไม่มีผล", this);
        }

        OnStarted?.Invoke();
        AnyStarted?.Invoke(this);
    }

    public void NotifyStarted(GameObject rootPlayer) { }

    // ===== ระหว่างโดนจับ =====

    private void HandleMash(InputAction.CallbackContext context)
    {
        if (!context.started || !IsMashing)
            return;

        // Pause / Game Over ตั้ง timeScale = 0 แต่ปุ่มยังส่ง event มา
        if (Time.timeScale <= 0f)
            return;

        _progress = Mathf.Min(1f, _progress + _progressPerPress);
        OnMash?.Invoke();
        Mashed?.Invoke();

        if (_progress >= 1f)
            Resolve(Result.Escaped);
    }

    private void Update()
    {
        if (!_running)
            return;

        // ท่าจบแล้ว (เล่นท่าดิ้นหลุด / ท่าแพ้ครบ) หรือมีอย่างอื่นปิด Interact ไปก่อน
        if (_player == null || !_player.IsPlaying)
        {
            if (!_resolved)
            {
                _resolved = true;
                if (_ghost != null)
                    _ghost.EndLoop(null);
            }
            Cleanup();
            return;
        }

        if (!IsMashing)
            return;

        float dt = Time.deltaTime;
        _elapsed += dt;
        _progress = Mathf.Max(0f, _progress - _decayPerSecond * dt);

        _damageTimer += dt;
        if (_damageTimer >= _damageInterval)
        {
            _damageTimer -= _damageInterval;
            ApplyDamage(_damagePerSecond * _damageInterval);
        }

        if (_resolved)
            return;

        if (_timeLimit > 0f && _elapsed >= _timeLimit)
            Resolve(Result.Failed);
    }

    private void HandlePlayerDead() => Resolve(Result.Failed, false);
    private void HandleGhostDead() => Resolve(Result.Escaped);

    private void Resolve(Result result, bool applyFailDamage = true)
    {
        if (!_running || _resolved)
            return;

        _resolved = true;
        _result = result;
        UnsubscribeInput();

        bool escaped = result == Result.Escaped;
        if (_player != null)
            _player.EndLoop(escaped ? _playerEscape : _playerFail);
        if (_ghost != null)
            _ghost.EndLoop(escaped ? _ghostEscape : _ghostFail);

        if (escaped)
        {
            _progress = 1f;
            OnEscaped?.Invoke();
        }
        else
        {
            if (applyFailDamage && _playerHealth != null)
            {
                if (_killOnFail)
                    _playerHealth.Kill();
                else
                    ApplyDamage(_failDamage);
            }
            OnFailed?.Invoke();
        }
    }

    private void ApplyDamage(float amount)
    {
        if (_playerHealth == null || amount <= 0f || _playerHealth.IsDead)
            return;

        if (_useHurtEvents)
        {
            _playerHealth.TakeDamage(amount);
        }
        else
        {
            if (_playerHealth.IgnoreDamage)
                return;

            // เคารพเลือดขั้นต่ำที่ State Machine ตั้งไว้ (SetHealthFloor) เหมือน TakeDamage
            float floor = _playerHealth.MaxHealth * _playerHealth.MinHealthPercent / 100f;
            float next = _playerHealth.CurrentHP - amount;
            if (_playerHealth.CurrentHP > floor)
                next = Mathf.Max(next, floor);

            if (next <= 0f)
                _playerHealth.Kill();
            else
                _playerHealth.SetCurrentHealth(next);
        }

        OnDamageTick?.Invoke();
    }

    private void Cleanup()
    {
        UnsubscribeInput();

        if (_playerHealth != null)
            _playerHealth.OnDead -= HandlePlayerDead;
        if (_ghostHealth != null)
            _ghostHealth.OnDead -= HandleGhostDead;

        bool wasRunning = _running;
        _running = false;
        _player = null;
        _playerHealth = null;
        _rootPlayer = null;

        if (wasRunning)
            AnyEnded?.Invoke(this);
    }

    private void UnsubscribeInput()
    {
        if (_input != null)
            _input.started -= HandleMash;
        _input = null;
    }
}
