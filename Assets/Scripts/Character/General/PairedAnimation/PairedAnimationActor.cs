using SDFcl.GamePlay.Interactable;
using UnityEngine;

public enum PairedActionType
{
    Climb,
    Glory
}

// interactor ที่ใช้กับระบบนี้ — State เรียกตอนท่าเริ่มจริง เพื่อยิง OnInteract
public interface IPairedInteractionSource
{
    void NotifyStarted(GameObject rootPlayer);
}

public readonly struct PairedRequest
{
    public readonly PairedActionType Type;
    public readonly PairedAnimationId Animation;
    public readonly Vector3 StartPosition;
    public readonly Quaternion StartRotation;
    public readonly Vector3 EndPosition;
    public readonly Quaternion EndRotation;
    public readonly IbaseInteractor Source;
    public readonly GameObject RootPlayer;

    // คู่ของท่า (เช่น ผีตอน Glory) — State ของฝ่ายนี้จะเป็นคนสั่งคู่ให้เข้าท่าตาม
    public readonly PairedAnimationActor Partner;
    public readonly PairedAnimationId PartnerAnimation;
    public readonly Vector3 PartnerPosition;
    public readonly Quaternion PartnerRotation;

    // เก็บเป็นตำแหน่ง ณ ตอนกด ไม่ใช่ Transform — จุดของ glory คำนวณสดจากที่ผีกับผู้เล่นยืนอยู่
    public PairedRequest(
        PairedActionType type,
        PairedAnimationId animation,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        IbaseInteractor source,
        GameObject rootPlayer)
        : this(type, animation, startPosition, startRotation, endPosition, endRotation, source, rootPlayer,
            null, null, Vector3.zero, Quaternion.identity)
    {
    }

    private PairedRequest(
        PairedActionType type,
        PairedAnimationId animation,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        IbaseInteractor source,
        GameObject rootPlayer,
        PairedAnimationActor partner,
        PairedAnimationId partnerAnimation,
        Vector3 partnerPosition,
        Quaternion partnerRotation)
    {
        Type = type;
        Animation = animation;
        StartPosition = startPosition;
        StartRotation = startRotation;
        EndPosition = endPosition;
        EndRotation = endRotation;
        Source = source;
        RootPlayer = rootPlayer;
        Partner = partner;
        PartnerAnimation = partnerAnimation;
        PartnerPosition = partnerPosition;
        PartnerRotation = partnerRotation;
    }

    // เล่นท่าอยู่กับที่ ไม่เคลื่อนต่อ (glory)
    public static PairedRequest InPlace(
        PairedActionType type,
        PairedAnimationId animation,
        Vector3 position,
        Quaternion rotation,
        IbaseInteractor source,
        GameObject rootPlayer)
    {
        return new PairedRequest(type, animation, position, rotation, position, rotation, source, rootPlayer);
    }

    public PairedRequest WithPartner(
        PairedAnimationActor partner,
        PairedAnimationId partnerAnimation,
        Vector3 partnerPosition,
        Quaternion partnerRotation)
    {
        return new PairedRequest(
            Type, Animation, StartPosition, StartRotation, EndPosition, EndRotation, Source, RootPlayer,
            partner, partnerAnimation, partnerPosition, partnerRotation);
    }

    public PairedRequest CreatePartnerRequest()
    {
        return InPlace(Type, PartnerAnimation, PartnerPosition, PartnerRotation, null, RootPlayer);
    }
}

// วางบนตัวเดียวกับ StateMachineController (ผู้เล่น = PlayerController, ผี = EnemeyController)
// ตัวนี้ไม่ทำอะไรเอง — ถือคำขอไว้ให้ Condition อ่าน แล้วรอ Action สั่ง Begin / Tick / Stop
public class PairedAnimationActor : MonoBehaviour
{
    [Tooltip("ว่างไว้ = ใช้ Animator บน GameObject นี้")]
    [SerializeField] private Animator _bodyAnimator;

    [Tooltip("Animator ของแขน FPS — ไม่มีก็เว้นว่าง")]
    [SerializeField] private Animator _armAnimator;

    [Tooltip("คำขอที่ไม่มี State มารับภายในเวลานี้จะถือว่าตกไป")]
    [SerializeField, Min(0.05f)] private float _requestTimeout = 0.5f;

    [Tooltip("ถ้า Animator ไม่เข้าท่าภายในเวลานี้ (เช่น พิมพ์ชื่อ state ผิด) จะข้ามไปจุดจบ กันตัวค้าง")]
    [SerializeField, Min(0.1f)] private float _stateEnterTimeout = 1f;

    private enum Phase { Idle, Play, Finished }

    private BaseLocomotion _locomotion;

    private PairedRequest _pending;
    private float _pendingTime = -1f;

    private PairedRequest _current;
    private Phase _phase = Phase.Idle;
    private bool _hasTakenControl;
    private bool _hasEnteredState;
    private int _bodyStateHash;
    private float _elapsed;
    private float _savedGravity;

    private Vector3 _snapFromPosition;
    private Quaternion _snapFromRotation;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private Vector3 _endPosition;
    private Quaternion _endRotation;

    public bool IsBusy => HasAnyPending || _phase != Phase.Idle;
    public bool IsFinished => _phase == Phase.Finished;
    public PairedRequest CurrentRequest => _current;

    private bool HasAnyPending =>
        _pendingTime >= 0f && Time.time - _pendingTime <= _requestTimeout;

    private void Awake()
    {
        _locomotion = GetComponent<BaseLocomotion>();

        if (_bodyAnimator == null)
            _bodyAnimator = GetComponent<Animator>();
    }

    private void OnDisable()
    {
        _pendingTime = -1f;
        Stop();
    }

    // interactor แค่ยื่นคำขอไว้ — จะได้เล่นหรือไม่ State Machine เป็นคนตัดสินผ่าน Condition
    public bool TryRequest(PairedRequest request)
    {
        if (IsBusy || request.Animation == null)
            return false;

        _pending = request;
        _pendingTime = Time.time;
        return true;
    }

    public bool HasPending(PairedActionType type) => HasAnyPending && _pending.Type == type;

    // Action เรียกตอนเข้า State — คืน false ถ้าไม่มีคำขอ (จะจบท่าทันทีให้ State ออกไป)
    public bool Begin()
    {
        if (!HasAnyPending)
        {
            Debug.LogWarning("[PairedAnimationActor] เข้า State แล้วแต่ไม่มีคำขอค้างอยู่ — จบท่าทันที", this);
            _phase = Phase.Finished;
            return false;
        }

        _current = _pending;
        _pending = default;
        _pendingTime = -1f;

        _snapFromPosition = transform.position;
        _snapFromRotation = transform.rotation;
        _startPosition = _current.StartPosition;
        _startRotation = FlatRotation(_current.StartRotation * Vector3.forward);
        _endPosition = _current.EndPosition;
        _endRotation = FlatRotation(_current.EndRotation * Vector3.forward);

        // ปิดแรงโน้มถ่วงกับ root motion ระหว่างท่า เพราะเราคุมตำแหน่ง/การหมุนเองทั้งหมด
        if (_locomotion != null)
        {
            _savedGravity = _locomotion.GetGravityMultiplier();
            _locomotion.SetGravityMultiplier(0f);
            _locomotion.SetRootMotionEnabled(false);
        }

        // สั่งท่าแบบเดียวกับ PlayAnimatorStateAction ของยูกิ แต่ชื่อท่ามาจาก asset ที่ interactor ส่งมา
        // ขอบแต่ละอันเลยใช้ท่าต่างกันได้ ทั้งที่ใช้ State ใน SM อันเดียว
        PairedAnimationId animation = _current.Animation;
        _bodyStateHash = Animator.StringToHash(animation.Body.StateName ?? string.Empty);
        PlayState(_bodyAnimator, animation.Body.Layer, animation.Body.StateName, animation.TransitionDuration);
        PlayState(_armAnimator, animation.Arm.Layer, animation.Arm.StateName, animation.TransitionDuration);

        _hasTakenControl = true;
        _hasEnteredState = false;
        _elapsed = 0f;
        _phase = Phase.Play;
        return true;
    }

    // Action เรียกทุกเฟรมจาก OnUpdate
    public void Tick()
    {
        if (_phase == Phase.Play)
            UpdatePlay();
    }

    // Action เรียกตอนออกจาก State เสมอ ไม่ว่าจะจบท่าเองหรือโดน State อื่นแทรก
    public void Stop()
    {
        if (_hasTakenControl)
        {
            PairedAnimationId animation = _current.Animation;
            if (animation != null)
            {
                PlayExit(_bodyAnimator, animation.Body, animation.TransitionDuration);
                PlayExit(_armAnimator, animation.Arm, animation.TransitionDuration);
            }

            if (_locomotion != null)
            {
                _locomotion.SetGravityMultiplier(_savedGravity);

                // BaseLocomotion ไม่มี getter ให้อ่านค่าเดิม เลยคืนเป็น true ซึ่งเป็นค่าเริ่มต้นของมัน
                _locomotion.SetRootMotionEnabled(true);
            }
        }

        _hasTakenControl = false;
        _phase = Phase.Idle;
        _current = default;
    }

    private void UpdatePlay()
    {
        PairedAnimationId animation = _current.Animation;
        _elapsed += Time.deltaTime;

        bool inState = TryGetStateTime(_bodyAnimator, animation.Body.Layer, _bodyStateHash, out float stateTime);
        if (inState)
            _hasEnteredState = true;

        // เวลาท่ามาจาก Animator ตรง ๆ — เปลี่ยนคลิปหรือความเร็วท่าใน Animator แล้วการเลื่อนตัวตามเอง
        float t = inState ? Mathf.Clamp01(stateTime) : (_hasEnteredState ? 1f : 0f);
        float up = animation.UpCurve.Evaluate(t);
        float forward = animation.ForwardCurve.Evaluate(t);

        // แยกแกนสูงกับแกนราบ ท่าปีนจะได้ขึ้นก่อนแล้วค่อยไปข้างหน้า ไม่ทะลุขอบเป็นเส้นตรง
        Vector3 delta = _endPosition - _startPosition;
        Vector3 pathPosition = _startPosition
            + Vector3.up * (delta.y * up)
            + new Vector3(delta.x, 0f, delta.z) * forward;
        Quaternion pathRotation = Quaternion.Slerp(_startRotation, _endRotation, forward);

        // Snap: ช่วงแรกค่อย ๆ ดึงจากจุดที่ยืนอยู่เข้าเส้นทาง ไปพร้อมกับที่ท่าเริ่มเล่น
        float snap = animation.SnapDuration > 0f
            ? Mathf.SmoothStep(0f, 1f, _elapsed / animation.SnapDuration)
            : 1f;

        SetPose(
            Vector3.Lerp(_snapFromPosition, pathPosition, snap),
            Quaternion.Slerp(_snapFromRotation, pathRotation, snap));

        if (_hasEnteredState && (!inState || stateTime >= 1f))
        {
            Finish();
        }
        else if (!_hasEnteredState && _elapsed > _stateEnterTimeout)
        {
            Debug.LogWarning(
                $"[PairedAnimationActor] Animator ไม่เข้าท่า '{animation.Body.StateName}' — ข้ามไปจุดจบ (เช็คชื่อ state / layer)", this);
            Finish();
        }
    }

    private void Finish()
    {
        SetPose(_endPosition, _endRotation);
        _phase = Phase.Finished;
    }

    private void SetPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        // CharacterController ไม่รู้ว่าเราย้าย transform เอง ต้อง sync ไม่งั้นเฟรมหน้าจะ Move จากตำแหน่งเก่าแล้วดีดกลับ
        Physics.SyncTransforms();
    }

    private Quaternion FlatRotation(Vector3 forward)
    {
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward.normalized, Vector3.up)
            : transform.rotation;
    }

    private static void PlayState(Animator animator, int layer, string stateName, float duration)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(layer, hash))
        {
            Debug.LogWarning($"[PairedAnimationActor] ไม่มี state '{stateName}' ใน layer {layer} ของ {animator.name}", animator);
            return;
        }

        animator.CrossFadeInFixedTime(hash, duration, layer);
    }

    private static void PlayExit(Animator animator, PairedAnimatorState state, float duration)
    {
        if (string.IsNullOrWhiteSpace(state.ExitStateName) || string.IsNullOrWhiteSpace(state.StateName))
            return;

        // ถ้า Animator ออกจากท่าไปเองแล้ว ไม่ต้องสั่งซ้ำ กันท่ายืน/เดินโดนเริ่มใหม่จนสะดุด
        if (!TryGetStateTime(animator, state.Layer, Animator.StringToHash(state.StateName), out _))
            return;

        PlayState(animator, state.Layer, state.ExitStateName, duration);
    }

    // นับทั้งตอนกำลังเฟดเข้าท่า (next) และตอนอยู่ในท่าแล้ว (current)
    private static bool TryGetStateTime(Animator animator, int layer, int stateHash, out float normalizedTime)
    {
        normalizedTime = 0f;

        if (animator == null || !animator.isActiveAndEnabled)
            return false;

        if (animator.IsInTransition(layer))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
            if (IsState(next, stateHash))
            {
                normalizedTime = next.normalizedTime;
                return true;
            }
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
        if (IsState(current, stateHash))
        {
            normalizedTime = current.normalizedTime;
            return true;
        }

        return false;
    }

    private static bool IsState(AnimatorStateInfo info, int stateHash) =>
        info.shortNameHash == stateHash || info.fullPathHash == stateHash;
}