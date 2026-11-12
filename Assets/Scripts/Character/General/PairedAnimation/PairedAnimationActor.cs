using System.Collections.Generic;
using SDFcl.GamePlay.Interactable;
using UnityEngine;
using UnityEngine.AI;

public enum PairedActionType
{
    Climb,
    Glory,
    Grab
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

    [Tooltip("สคริปต์ที่ขยับท่าทางทับแอนิเมชัน เช่น Look At IK / Foot IK — ปิดไว้ระหว่างเล่นท่า แล้วเปิดคืนให้เองตอนจบ\n" +
             "ถ้าไม่ปิด หัว/คอ/สะโพกจะเยื้องไม่เหมือนกันทุกรอบ มือเลยจับไม่ตรง")]
    [SerializeField] private Behaviour[] _disableWhilePlaying = new Behaviour[0];

    private enum Phase { Idle, Prepare, Play, Finished }

    private readonly List<Behaviour> _disabledByMe = new List<Behaviour>();

    private BaseLocomotion _locomotion;
    private Animator _rootAnimator;

    private PairedRequest _pending;
    private float _pendingTime = -1f;

    private PairedRequest _current;
    private Phase _phase = Phase.Idle;
    private bool _hasTakenControl;
    private bool _controlsTransform;
    private bool _hasEnteredState;
    private int _bodyStateHash;
    private float _elapsed;
    private bool _locomotionChanged;
    private float _savedGravity;
    private LocomotionMoveMode _savedMoveMode;
    private bool _moveByAnimation;
    private bool _bodyRootMotionDisabled;

    // ท่าที่กำลังเล่นอยู่จริง — ปกติคือท่าของคำขอ แต่หลัง EndLoop จะเป็นท่าจบ (เช่น ท่าดิ้นหลุด)
    private PairedAnimationId _playing;
    private bool _loopReleased;

    private Vector3 _snapFromPosition;
    private Quaternion _snapFromRotation;
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    // ตำแหน่ง/ทิศที่ท่าพาไปจริง — ยึดค่านี้ทุกเฟรม สคริปต์อื่นจะได้หมุนหรือดันตัวทับไม่ได้ระหว่างท่า
    private Vector3 _posePosition;
    private Quaternion _poseRotation;

    private NavMeshAgent _agent;
    private bool _agentPaused;
    private bool _savedAgentUpdatePosition;
    private bool _savedAgentUpdateRotation;

    private readonly List<(Collider mine, Collider theirs)> _ignoredPairs = new List<(Collider, Collider)>();
    private CharacterController _ownController;
    private CharacterController _partnerController;
    private bool _savedOwnDetect;
    private bool _savedPartnerDetect;
    private bool _ignoringPartner;
    private bool _restoreWhenApart;

    public bool IsBusy => HasAnyPending || _phase != Phase.Idle;
    public bool IsPreparing => _phase == Phase.Prepare;
    public bool IsFinished => _phase == Phase.Finished;
    public bool IsPlayingAnimation => _phase == Phase.Play;

    // ช่วงดูดตัว / หันเข้าหาเสร็จแล้ว
    public bool IsSnapDone =>
        _phase != Phase.Prepare || _current.Animation == null || _elapsed >= _current.Animation.SnapDuration;

    // ท่า Loop กำลังวนค้างอยู่ รอใครสักคนสั่ง EndLoop
    public bool IsLooping => _phase == Phase.Play && _playing != null && _playing.Loop && !_loopReleased;
    public PairedRequest CurrentRequest => _current;

    private bool HasAnyPending =>
        _pendingTime >= 0f && Time.time - _pendingTime <= _requestTimeout;

    private void Awake()
    {
        _locomotion = GetComponent<BaseLocomotion>();
        _rootAnimator = GetComponent<Animator>();
        _agent = GetComponentInParent<NavMeshAgent>();

        if (_bodyAnimator == null)
            _bodyAnimator = _rootAnimator;
    }

    private void OnDisable()
    {
        _pendingTime = -1f;
        Stop();
        RestorePartnerCollisions();
    }

    private void Update()
    {
        // ท่าจบแล้ว รอให้ตัวแยกออกจากกันก่อนค่อยกลับมาชนกัน ไม่งั้นตัวจะเด้งกระเด็น
        if (_restoreWhenApart && !IsTouchingPartner())
            RestorePartnerCollisions();
    }

    // ระหว่างเล่นท่า ขยับตัวตามแอนิเมชันตรง ๆ ไม่ผ่าน CharacterController
    // ไม่งั้นตัวจะไปชนรากไม้ / เนิน / ผีตัวอื่นแล้วเยื้อง ทำให้มือจับไม่ตรงคอ
    private void OnAnimatorMove()
    {
        if (_rootAnimator == null)
            return;

        if (_hasTakenControl)
        {
            // Move Mode = None: ไม่เอา Root Motion และไม่ยึดตัว ปล่อยให้ State ขยับตัวเอง
            if (!_controlsTransform)
                return;

            if (_moveByAnimation && _phase == Phase.Play)
            {
                _posePosition += _rootAnimator.deltaPosition;
                _poseRotation *= _rootAnimator.deltaRotation;
            }

            transform.SetPositionAndRotation(_posePosition, _poseRotation);
            return;
        }

        // ตัวที่ไม่มี BaseLocomotion ให้ Animator ขยับตามปกติเหมือนเดิม
        if (_locomotion == null && _rootAnimator.applyRootMotion)
            _rootAnimator.ApplyBuiltinRootMotion();
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
        // ตั้งที่ Move Mode ในท่า — None = ไม่แตะตำแหน่งตัวเลย ไม่ดูดตัว ไม่ใช้ Root Motion ให้ State ขยับเอง
        _controlsTransform = _current.Animation.MoveMode == PairedMoveMode.RootMotion;

        _snapFromPosition = transform.position;
        _snapFromRotation = transform.rotation;
        _posePosition = _snapFromPosition;
        _poseRotation = _snapFromRotation;
        _startPosition = _current.StartPosition;
        _startRotation = FlatRotation(_current.StartRotation * Vector3.forward);

        // ระหว่างเล่นท่า ตัวเรากับคู่ต้องทะลุกันได้ ไม่งั้น CharacterController จะชนกันจนจับคอไม่ถึง
        IgnorePartnerCollisions(_current.Partner);
        DisableExtras();

        if (_controlsTransform)
        {
            PauseAgent();

            // ปิดแรงโน้มถ่วง ตั้งโหมด External ให้ BaseLocomotion หยุดขยับตัว แล้วสคริปต์นี้คุมเองทั้งหมด
            // ไม่ล็อกอินพุตตรงนี้ — State ของตัวละครเป็นคนจัดการเอง
            if (_locomotion != null)
            {
                _savedGravity = _locomotion.GetGravityMultiplier();
                _locomotion.SetGravityMultiplier(0f);
                _savedMoveMode = _locomotion.MoveMode;
                _locomotion.MoveMode = LocomotionMoveMode.External;
                _locomotionChanged = true;
            }
        }
        else
        {
            DisableBodyRootMotion();
        }

        PairedAnimationId animation = _current.Animation;
        _bodyStateHash = Animator.StringToHash(animation.Body.StateName ?? string.Empty);

        _hasTakenControl = true;
        _hasEnteredState = false;
        _moveByAnimation = false;
        _elapsed = 0f;
        _phase = Phase.Prepare;

        if (!deferAnimation)
            StartPlayback();

        return true;
    }

    // เริ่มเล่นแอนิเมชันหลังจากตัวละครและกล้อง Snap เข้าที่แล้ว
    public void StartPlayback()
    {
        if (_phase != Phase.Prepare)
            return;

        // ยึดจุดเริ่มต้นให้ตรงเป๊ะก่อนปล่อยให้ Root Motion ทำงาน
        if (_controlsTransform)
            SetPose(_startPosition, _startRotation);
        _moveByAnimation = _controlsTransform;

        _playing = _current.Animation;
        _loopReleased = false;
        _bodyStateHash = Animator.StringToHash(_playing.Body.StateName ?? string.Empty);
        PlayState(_bodyAnimator, _playing.Body.Layer, _playing.Body.StateName, _playing.TransitionDuration);
        PlayState(_armAnimator, _playing.Arm.Layer, _playing.Arm.StateName, _playing.TransitionDuration);

        _hasEnteredState = false;
        _elapsed = 0f;
        _phase = Phase.Play;
    }

    // เลื่อนตัวระหว่างเล่นท่า (เช่น ดึงคอผีเข้าหามือ) — ต้องผ่านตรงนี้ ไม่งั้นเฟรมถัดไปจะโดนยึดกลับ
    public void Nudge(Vector3 offset)
    {
        if (!_moveByAnimation || _phase != Phase.Play)
            return;

        _posePosition += offset;
        transform.position = _posePosition;
    }

    // ปล่อยท่า Loop — ใส่ท่าจบ (เช่น ดิ้นหลุด) จะเล่นต่อจนจบแล้วค่อย Finished / ว่าง = จบทันที
    public void EndLoop(PairedAnimationId finishAnimation)
    {
        if (_phase == Phase.Prepare)
        {
            Finish();
            return;
        }

        if (_phase != Phase.Play || _loopReleased)
            return;

        _loopReleased = true;

        if (finishAnimation == null || string.IsNullOrWhiteSpace(finishAnimation.Body.StateName))
        {
            Finish();
            return;
        }

        _playing = finishAnimation;
        _bodyStateHash = Animator.StringToHash(finishAnimation.Body.StateName);
        PlayState(_bodyAnimator, finishAnimation.Body.Layer, finishAnimation.Body.StateName, finishAnimation.TransitionDuration);
        PlayState(_armAnimator, finishAnimation.Arm.Layer, finishAnimation.Arm.StateName, finishAnimation.TransitionDuration);
        _hasEnteredState = false;
        _elapsed = 0f;
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
            PairedAnimationId animation = _playing != null ? _playing : _current.Animation;
            if (animation != null && _phase != Phase.Prepare)
            {
                PlayExit(_bodyAnimator, animation.Body, animation.TransitionDuration);
                PlayExit(_armAnimator, animation.Arm, animation.TransitionDuration);
            }

            if (_locomotionChanged && _locomotion != null)
            {
                _locomotion.SetGravityMultiplier(_savedGravity);
                _locomotion.MoveMode = _savedMoveMode;
            }

            RestoreBodyRootMotion();

            // ให้ CharacterController รู้ตำแหน่งใหม่ที่แอนิเมชันพามา
            if (_controlsTransform)
                Physics.SyncTransforms();
            RestoreExtras();
        }

        ResumeAgent();

        if (_ignoringPartner)
            _restoreWhenApart = true;

        _hasTakenControl = false;
        _controlsTransform = false;
        _locomotionChanged = false;
        _moveByAnimation = false;
        _phase = Phase.Idle;
        _current = default;
        _playing = null;
        _loopReleased = false;
    }

    private void UpdatePrepare()
    {
        PairedAnimationId animation = _current.Animation;
        _elapsed += Time.deltaTime;

        // Move Mode = None ไม่ดูดตัว แค่รอเวลา Snap Duration ให้กล้องหันเสร็จ
        if (!_controlsTransform)
            return;

        float snap = animation.SnapDuration > 0f
            ? Mathf.SmoothStep(0f, 1f, _elapsed / animation.SnapDuration)
            : 1f;

        SetPose(
            Vector3.Lerp(_snapFromPosition, _startPosition, snap),
            Quaternion.Slerp(_snapFromRotation, _startRotation, snap));
    }

    private void UpdatePlay()
    {
        PairedAnimationId animation = _playing;
        _elapsed += Time.deltaTime;

        bool inState = TryGetStateTime(_bodyAnimator, animation.Body.Layer, _bodyStateHash, out float stateTime);
        if (inState)
            _hasEnteredState = true;

        // ท่า Loop ไม่จบเอง ต้องรอ EndLoop
        bool holdLoop = animation.Loop && !_loopReleased;

        if (_hasEnteredState && !holdLoop && (!inState || stateTime >= 1f))
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

    // ---------- Root Motion ----------

    // Animator ที่อยู่ตัวลูก OnAnimatorMove ของตัวนี้คุมไม่ถึง ต้องปิด Root Motion ตรง ๆ แล้วเปิดคืนตอนจบ
    private void DisableBodyRootMotion()
    {
        if (_bodyAnimator == null || _bodyAnimator == _rootAnimator || !_bodyAnimator.applyRootMotion)
            return;

        _bodyAnimator.applyRootMotion = false;
        _bodyRootMotionDisabled = true;
    }

    private void RestoreBodyRootMotion()
    {
        if (_bodyRootMotionDisabled && _bodyAnimator != null)
            _bodyAnimator.applyRootMotion = true;
        _bodyRootMotionDisabled = false;
    }

    // ---------- ทะลุกันชั่วคราว ----------

    private void IgnorePartnerCollisions(PairedAnimationActor partner)
    {
        RestorePartnerCollisions();
        if (partner == null || partner == this)
            return;

        _ignoringPartner = true;

        Collider[] theirColliders = partner.GetComponentsInChildren<Collider>();
        foreach (Collider mine in GetComponentsInChildren<Collider>())
        {
            if (!IsSolid(mine))
                continue;

            foreach (Collider theirs in theirColliders)
            {
                if (!IsSolid(theirs) || Physics.GetIgnoreCollision(mine, theirs))
                    continue;

                Physics.IgnoreCollision(mine, theirs, true);
                _ignoredPairs.Add((mine, theirs));
            }
        }

        // CharacterController สองตัวชนกันเองอีกชั้น ต้องปิดตรงนี้ด้วย
        _ownController = GetComponent<CharacterController>();
        _partnerController = partner.GetComponent<CharacterController>();

        if (_ownController != null)
        {
            _savedOwnDetect = _ownController.detectCollisions;
            _ownController.detectCollisions = false;
        }

        if (_partnerController != null)
        {
            _savedPartnerDetect = _partnerController.detectCollisions;
            _partnerController.detectCollisions = false;
        }
    }

    private bool IsTouchingPartner()
    {
        foreach ((Collider mine, Collider theirs) in _ignoredPairs)
        {
            if (IsSolid(mine) && IsSolid(theirs) && mine.bounds.Intersects(theirs.bounds))
                return true;
        }
        return false;
    }

    private void RestorePartnerCollisions()
    {
        foreach ((Collider mine, Collider theirs) in _ignoredPairs)
        {
            if (IsSolid(mine) && IsSolid(theirs))
                Physics.IgnoreCollision(mine, theirs, false);
        }
        _ignoredPairs.Clear();

        if (_ownController != null)
            _ownController.detectCollisions = _savedOwnDetect;
        if (_partnerController != null)
            _partnerController.detectCollisions = _savedPartnerDetect;

        _ownController = null;
        _partnerController = null;
        _ignoringPartner = false;
        _restoreWhenApart = false;
    }

    private static bool IsSolid(Collider collider) =>
        collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger;

    // ---------- ปิดสคริปต์ IK ชั่วคราว ----------

    private void DisableExtras()
    {
        foreach (Behaviour behaviour in _disableWhilePlaying)
        {
            if (behaviour == null || !behaviour.enabled)
                continue;
            behaviour.enabled = false;
            _disabledByMe.Add(behaviour);
        }
    }

    private void RestoreExtras()
    {
        foreach (Behaviour behaviour in _disabledByMe)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }
        _disabledByMe.Clear();
    }

    // ---------- NavMesh ----------

    // NavMeshAgent ชอบดึงตำแหน่ง / หันตัวกลับ ปิดไว้ระหว่างท่า (Move Mode = None ไม่แตะ)
    private void PauseAgent()
    {
        if (_agentPaused || _agent == null || !_agent.enabled)
            return;

        _agentPaused = true;
        _savedAgentUpdatePosition = _agent.updatePosition;
        _savedAgentUpdateRotation = _agent.updateRotation;
        _agent.updatePosition = false;
        _agent.updateRotation = false;
    }

    private void ResumeAgent()
    {
        if (!_agentPaused)
            return;
        _agentPaused = false;

        if (_agent == null || !_agent.enabled)
            return;

        if (_agent.isOnNavMesh)
            _agent.nextPosition = _agent.transform.position;

        _agent.updatePosition = _savedAgentUpdatePosition;
        _agent.updateRotation = _savedAgentUpdateRotation;
    }

    // ---------- Helper ----------

    private void SetPose(Vector3 position, Quaternion rotation)
    {
        _posePosition = position;
        _poseRotation = rotation;
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