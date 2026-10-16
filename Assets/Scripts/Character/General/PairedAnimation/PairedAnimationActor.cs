using SDFcl.GamePlay.Interactable;
using UnityEngine;

public enum PairedActionType
{
    Climb,
    Glory
}

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

    public readonly PairedAnimationActor Partner;
    public readonly PairedAnimationId PartnerAnimation;
    public readonly Vector3 PartnerPosition;
    public readonly Quaternion PartnerRotation;

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

    private enum Phase { Idle, Prepare, Play, Finished }

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
    private LocomotionMoveMode _savedMoveMode;

    private Vector3 _snapFromPosition;
    private Quaternion _snapFromRotation;
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    public bool IsBusy => HasAnyPending || _phase != Phase.Idle;
    public bool IsPreparing => _phase == Phase.Prepare;
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

    public bool TryRequest(PairedRequest request)
    {
        if (IsBusy || request.Animation == null)
            return false;

        _pending = request;
        _pendingTime = Time.time;
        return true;
    }

    public bool HasPending(PairedActionType type) => HasAnyPending && _pending.Type == type;

    public bool Begin() => Begin(false);

    public bool Begin(bool deferAnimation)
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

        // ช่วง Prepare (Snap) ปิดแรงโน้มถ่วงและตั้งโหมดเป็น External เพื่อเลื่อนตัวละครเข้าจุดเริ่มด้วยโค้ด
        if (_locomotion != null)
        {
            _savedGravity = _locomotion.GetGravityMultiplier();
            _locomotion.SetGravityMultiplier(0f);
            _savedMoveMode = _locomotion.MoveMode;
            _locomotion.MoveMode = LocomotionMoveMode.External;
        }

        PairedAnimationId animation = _current.Animation;
        _bodyStateHash = Animator.StringToHash(animation.Body.StateName ?? string.Empty);

        _hasTakenControl = true;
        _hasEnteredState = false;
        _elapsed = 0f;
        _phase = Phase.Prepare;

        if (!deferAnimation)
            StartPlayback();

        return true;
    }

    // เริ่มเล่นแอนิเมชันด้วย FullRootMotion หลังจากตัวละครและกล้อง Snap เข้าที่แล้ว
    public void StartPlayback()
    {
        if (_phase != Phase.Prepare)
            return;

        // ยึดจุดเริ่มต้นให้ตรงเป๊ะก่อนปล่อยให้ Root Motion ทำงาน
        SetPose(_startPosition, _startRotation);

        // สลับเป็น FullRootMotion เพื่อให้ BaseLocomotion.OnAnimatorMove ขยับทั้ง Position และ Rotation ตามคลิป
        if (_locomotion != null)
        {
            _locomotion.MoveMode = LocomotionMoveMode.FullRootMotion;
        }

        PairedAnimationId animation = _current.Animation;
        PlayState(_bodyAnimator, animation.Body.Layer, animation.Body.StateName, animation.TransitionDuration);
        PlayState(_armAnimator, animation.Arm.Layer, animation.Arm.StateName, animation.TransitionDuration);

        _hasEnteredState = false;
        _elapsed = 0f;
        _phase = Phase.Play;
    }

    public void Tick()
    {
        if (_phase == Phase.Prepare)
            UpdatePrepare();
        else if (_phase == Phase.Play)
            UpdatePlay();
    }

    public void Stop()
    {
        if (_hasTakenControl)
        {
            PairedAnimationId animation = _current.Animation;
            if (animation != null && _phase != Phase.Prepare)
            {
                PlayExit(_bodyAnimator, animation.Body, animation.TransitionDuration);
                PlayExit(_armAnimator, animation.Arm, animation.TransitionDuration);
            }

            if (_locomotion != null)
            {
                _locomotion.SetGravityMultiplier(_savedGravity);
                _locomotion.MoveMode = _savedMoveMode;
            }
        }

        _hasTakenControl = false;
        _phase = Phase.Idle;
        _current = default;
    }

    private void UpdatePrepare()
    {
        PairedAnimationId animation = _current.Animation;
        _elapsed += Time.deltaTime;

        float snap = animation.SnapDuration > 0f
            ? Mathf.SmoothStep(0f, 1f, _elapsed / animation.SnapDuration)
            : 1f;

        SetPose(
            Vector3.Lerp(_snapFromPosition, _startPosition, snap),
            Quaternion.Slerp(_snapFromRotation, _startRotation, snap));
    }

    private void UpdatePlay()
    {
        PairedAnimationId animation = _current.Animation;
        _elapsed += Time.deltaTime;

        bool inState = TryGetStateTime(_bodyAnimator, animation.Body.Layer, _bodyStateHash, out float stateTime);
        if (inState)
            _hasEnteredState = true;

        // ไม่ใช้ UpCurve / ForwardCurve ขยับตัวละครแล้ว ปล่อยให้ FullRootMotion ใน BaseLocomotion ขยับ 100%

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
        _phase = Phase.Finished;
    }

    private void SetPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
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

        if (!TryGetStateTime(animator, state.Layer, Animator.StringToHash(state.StateName), out _))
            return;

        PlayState(animator, state.Layer, state.ExitStateName, duration);
    }

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