using SDFcl.GamePlay.Interactable;
using UnityEngine;

[RequireComponent(typeof(Iinteractor), typeof(IbaseInteractor))]
public class GloryInteractAddition : MonoBehaviour, IPairedInteractionSource
{
    [SerializeField] private PairedAnimationActor _victim;
    [SerializeField] private PairedAnimationId _playerAnimation;
    [SerializeField] private PairedAnimationId _victimAnimation;

    [Header("ตำแหน่งยืน (แก้ตอน Play ได้ มีผลกับการกด E ครั้งถัดไป)")]
    [Tooltip("ผู้เล่นยืนห่างผีกี่เมตร — มือเอื้อมไม่ถึงคอ ลดค่า / มือเลยคอไป เพิ่มค่า")]
    [SerializeField, Min(0f)] private float _distance = 1.2f;

    [Tooltip("เลื่อนผู้เล่นซ้าย-ขวา (เมตร) — ปกติปล่อย 0 ไว้ ระบบดึงคอด้านล่างจัดให้เอง")]
    [SerializeField] private float _sideOffset = 0f;

    [Tooltip("เลื่อนผู้เล่นขึ้น-ลง (เมตร) — มือจับต่ำกว่าคอ ใส่ค่าบวก / สูงกว่าคอ ใส่ค่าลบ")]
    [SerializeField] private float _heightOffset = 0f;

    [Header("ดึงคอผีเข้าหามืออัตโนมัติ")]
    [Tooltip("กระดูกคอของผี — ลากจาก Hierarchy ของผีมาใส่\nเว้นว่าง = ใช้กระดูกคอของ Animator แบบ Humanoid (ถ้ามี)")]
    [SerializeField] private Transform _aimPoint;

    [Tooltip("มือจับลงตรงไหนของจอ (แนวนอน) — 0.5 = กลางจอ")]
    [SerializeField, Range(0f, 1f)] private float _handScreenX = 0.5f;

    [Tooltip("ท่าเริ่มแล้วกี่วินาทีถึงหยุดดึง — ให้เลยจังหวะที่มือแตะคอไปนิดนึง")]
    [SerializeField, Min(0f)] private float _alignTime = 0.35f;

    [Tooltip("ดึงเร็วแค่ไหน — สูง = เข้าที่ไว")]
    [SerializeField, Min(0.1f)] private float _alignSpeed = 18f;

    [Tooltip("ดึงได้ไกลสุดกี่เมตร กันผีวาร์ปถ้ามีอะไรผิดพลาด")]
    [SerializeField, Min(0f)] private float _maxAlign = 0.3f;

    [Tooltip("บอกใน Console ว่าแต่ละรอบดึงผีไปกี่ ซม.")]
    [SerializeField] private bool _logAlign;

    private Iinteractor _interactor;
    private IbaseInteractor _baseInteractor;
    private PairedAnimationPlayer _activePlayer;   // ผู้เล่นที่กำลังเล่นท่าจาก Interactor ตัวนี้

    private Transform _aimTransform;
    private bool _aligning;
    private float _alignElapsed;
    private float _alignTotal;

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
        _baseInteractor = GetComponent<IbaseInteractor>();
    }

    private void OnEnable()
    {
        // รับคำสั่งตอนผู้เล่นกด E
        _interactor.OnInteract += HandleInteract;
        _interactor.AddCanInteractModifier(IsNotPlaying);
        _interactor.AddCanCancelInteractModifier(IsNotPlaying);
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        _interactor.RemoveCanInteractModifier(IsNotPlaying);
        _interactor.RemoveCanCancelInteractModifier(IsNotPlaying);
        _aligning = false;
    }

    // BaseInteractor มาถาม: กดได้ไหม / ยกเลิกได้ไหม
    // ระหว่างเล่นท่า Interactor ขอยกเลิกเองไม่ได้ (เช่น ButtonInteractor ที่ยกเลิกทันทีหลังกด)
    // ท่าจบแล้ว PairedAnimationPlayer ยกเลิกด้วย force จึงไม่ติดตัวนี้
    private bool IsNotPlaying(GameObject rootPlayer)
    {
        return _activePlayer == null || !_activePlayer.IsPlaying;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        PairedAnimationPlayer player = rootplayer.GetComponentInChildren<PairedAnimationPlayer>();
        if (player == null || _victim == null)
        {
            Debug.LogWarning($"[GloryInteractAddition] {name} หา PairedAnimationPlayer บนผู้เล่น หรือ Victim ไม่เจอ", this);
            return;
        }

        // ผีหันหาผู้เล่น ผู้เล่นยืนตรงหน้าผีแล้วหันเข้าหา
        Vector3 victimPosition = _victim.transform.position;
        Vector3 toPlayer = player.transform.position - victimPosition;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = Vector3.ProjectOnPlane(_victim.transform.forward, Vector3.up);
        toPlayer.Normalize();

        Quaternion victimRotation = Quaternion.LookRotation(toPlayer);
        Quaternion playerRotation = Quaternion.LookRotation(-toPlayer);

        // ยืนระดับเดียวกับผีเสมอ ท่าจะได้ตรงกันแม้พื้นมีรากไม้ / เนิน (เท้าจมดินนิดหน่อยมองไม่เห็นในมุม FPS)
        Vector3 playerPosition = victimPosition
            + toPlayer * _distance
            + playerRotation * Vector3.right * _sideOffset
            + Vector3.up * _heightOffset;

        // ผู้เล่นเล่นท่าเองผ่าน Component (อยู่ใน PlayerInteract_State) แล้วสั่งผีให้เล่นตาม
        // ถ้าเริ่มไม่ได้ PairedAnimationPlayer จะยกเลิก Interact ให้เองในเฟรมถัดไป
        PairedRequest request = PairedRequest
            .InPlace(PairedActionType.Glory, _playerAnimation, playerPosition, playerRotation, _baseInteractor, rootplayer)
            .WithPartner(_victim, _victimAnimation, victimPosition, victimRotation);

        _activePlayer = player;
        player.Play(request);

        BeginAlign();
    }

    public void NotifyStarted(GameObject rootPlayer) { }

    // ---------- ดึงคอผีเข้าหามือ ----------
    // มือแขน FPS จับลงที่เดิมบนจอทุกครั้ง แต่คอผีเยื้องไปตามมุมที่เดินเข้าหา / ท่าที่ผีเซอยู่
    // เลยวัดระยะเยื้องจริงทุกเฟรมช่วงที่มือกำลังเอื้อม แล้วค่อยๆ เลื่อนผีให้คอมาอยู่ตรงจุดที่มือจะจับ

    private void BeginAlign()
    {
        _aimTransform = ResolveAimPoint();
        _aligning = _aimTransform != null;
        _alignElapsed = 0f;
        _alignTotal = 0f;

        if (_aimTransform == null)
            Debug.LogWarning($"[GloryInteractAddition] {name} ยังไม่ได้ใส่ Aim Point (กระดูกคอผี) — ข้ามการดึงคอ", this);
    }

    private Transform ResolveAimPoint()
    {
        if (_aimPoint != null)
            return _aimPoint;

        Animator animator = _victim.GetComponent<Animator>();
        return animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Neck) : null;
    }

    private void LateUpdate()
    {
        if (!_aligning)
            return;

        if (_activePlayer == null || !_activePlayer.IsPlaying)
        {
            StopAlign();
            return;
        }

        // รอให้ท่าของทั้งคู่เริ่มจริงก่อน (ผ่านช่วงดูดตัว / หันกล้อง)
        if (!_activePlayer.Actor.IsPlayingAnimation || !_victim.IsPlayingAnimation)
            return;

        _alignElapsed += Time.deltaTime;
        if (_alignElapsed > _alignTime)
        {
            StopAlign();
            return;
        }

        Camera view = Camera.main;
        if (view == null || _aimTransform == null)
            return;

        Transform cam = view.transform;
        float tanHalfWidth = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * view.aspect;
        Vector3 handRay = (cam.forward + cam.right * ((_handScreenX * 2f - 1f) * tanHalfWidth)).normalized;

        Vector3 toNeck = _aimTransform.position - cam.position;
        float depth = Vector3.Dot(toNeck, handRay);
        if (depth < 0.05f)
            return;

        Vector3 side = Vector3.ProjectOnPlane(cam.right, Vector3.up);
        if (side.sqrMagnitude < 0.0001f)
            return;
        side.Normalize();

        // บวก = คออยู่ทางขวาของจุดที่มือจะจับ ต้องเลื่อนผีไปทางซ้าย
        float error = Vector3.Dot(toNeck - handRay * depth, side);
        float step = error * (1f - Mathf.Exp(-_alignSpeed * Time.deltaTime));
        float next = Mathf.Clamp(_alignTotal - step, -_maxAlign, _maxAlign);

        _victim.Nudge(side * (next - _alignTotal));
        _alignTotal = next;
    }

    private void StopAlign()
    {
        if (!_aligning)
            return;
        _aligning = false;

        if (_logAlign)
            Debug.Log($"[GloryInteractAddition] {name} ดึงผีไปทาง{(_alignTotal >= 0f ? "ขวา" : "ซ้าย")} {Mathf.Abs(_alignTotal) * 100f:0.0} ซม.", this);
    }
}