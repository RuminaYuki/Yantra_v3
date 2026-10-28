using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationProgressDriver : MonoBehaviour
{
    /// <summary>
    /// ส่งเป็น progress เพื่อสั่งให้เล่น state นั้นจนจบด้วย speed ปกติ (ไม่คุมตาม progress)
    /// คำสั่ง state อื่นที่ส่งมาระหว่างนั้นจะถูกเก็บไว้ แล้วค่อยเล่นตอน state นี้เล่นจบ
    /// </summary>
    public const float PlayFull = -1f;

    [SerializeField] Animator _animator;
    [SerializeField] BoolEventChannelSO _aPD_SetEnable_AEC;
    [SerializeField] AnimationProgressDriverAdvEventChannal _aPD_AEC;

    [Header("Setting")]
    [SerializeField] float speedMultiple = 1f;

    [Header("Catch Up")]
    [Tooltip("Seconds the animation should take to close the gap to the real progress. Smaller = snappier.")]
    [SerializeField, Min(0.01f)] float catchUpTime = 0.25f;
    [Tooltip("Gap (normalized) treated as already caught up.")]
    [SerializeField, Min(0f)] float arriveThreshold = 0.002f;

    [Header("Smoothing")]
    [Tooltip("เวลาที่ใช้ไล่ speed ไปหาค่าเป้าหมาย (ยิ่งมากยิ่งนุ่ม แต่ตอบสนองช้าลง)")]
    [SerializeField, Min(0f)] float speedSmoothTime = 0.1f;
    [Tooltip("จำกัด speed สูงสุด กันกระชากเวลา progress กระโดดไกล")]
    [SerializeField, Min(0f)] float maxSpeed = 3f;
    [Tooltip("เวลาที่ใช้คืน speed กลับเป็น 1 หลังปิด driver")]
    [SerializeField, Min(0f)] float releaseSmoothTime = 0.15f;

    //เวลารอก่อนปรับ AnimatorUpdateMode กลับเป็น Normal หลังปิด driver (กันกระชากเวลา progress กระโดดไกล)
    [Header("Release Delay")]
    [SerializeField, Min(0f)] float releaseDelay = 0.1f;

    private int _layerIndex;
    private int _stateHash;
    private bool _active = false;
    private bool _releasing = false;
    private float _progress = 0;
    private float _currentSpeed = 1f;
    private float _speedVelocity = 0f;
    private float _stateLength = 1f;
    AnimatorStateInfo stateInfo;
    private bool _isCurrentStateValid = true;

    // state ที่กำลังเล่นจนจบ (PlayFull) และคำสั่งที่รอเล่นต่อ
    private bool _playingFull = false;
    private bool _hasPending = false;
    private string _pendingStateName;
    private int _pendingLayerIndex;
    private float _pendingProgress;
    private float _pendingFadeDuration;
    private AnimatorUpdateMode _pendingUpdateMode;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (_aPD_AEC != null)
        {
            _aPD_AEC.Raised += HandleSetProgress;
        }
        if (_aPD_SetEnable_AEC != null)
        {
            _aPD_SetEnable_AEC.Raised += HandleSetEnable;
        }
    }

    private void OnDisable()
    {
        if (_aPD_AEC != null)
        {
            _aPD_AEC.Raised -= HandleSetProgress;
        }
        if (_aPD_SetEnable_AEC != null)
        {
            _aPD_SetEnable_AEC.Raised -= HandleSetEnable;
        }

        // กันค้าง speed แปลกๆ ถ้า object ถูกปิดกลางคัน
        if (_animator != null) _animator.speed = 1f;
        _currentSpeed = 1f;
        _speedVelocity = 0f;
        _releasing = false;
        _playingFull = false;
        _hasPending = false;
    }

    private void Update()
    {
        // PlayFull ต้องเล่นจนจบ แม้โดนปิดระหว่างนั้น ไม่งั้นท่าที่รอไว้จะไม่ได้เล่น
        if (_active || _playingFull)
        {
            SetProgress();
        }
        else if (_releasing)
        {
            ReleaseSpeed();
        }
    }

    private void SetProgress()
    {
        // เล่น state แบบ PlayFull อยู่ ปล่อยเล่นปกติจนจบ แล้วค่อยเล่นคำสั่งที่รออยู่
        if (_playingFull)
        {
            PassThrough();

            if (!IsPlayFullFinished()) return;

            _playingFull = false;
            if (_hasPending)
            {
                _hasPending = false;
                HandleSetProgress(_pendingStateName, _pendingLayerIndex, _pendingProgress,
                                  _pendingFadeDuration, _pendingUpdateMode);
            }
            return;
        }

        // ระหว่าง CrossFade ไม่คุม speed ปล่อยให้ transition เดินปกติจนจบ
        if (_animator.IsInTransition(_layerIndex))
        {
            PassThrough();
            return;
        }

        stateInfo = _animator.GetCurrentAnimatorStateInfo(_layerIndex); // อ่านสดทุก frame

        // ทำงานเฉพาะตอนอยู่ใน state ที่ได้รับมาเท่านั้น
        if (stateInfo.shortNameHash != _stateHash && stateInfo.fullPathHash != _stateHash)
        {
            PassThrough();
            return;
        }

        float animProgress = stateInfo.normalizedTime;
        CacheStateLength(stateInfo);

        float targetSpeed = CalculatSpeed(animProgress);
        _currentSpeed = Smooth(_currentSpeed, targetSpeed, speedSmoothTime);

        // Never step past the target this frame (replaces the hard stop to 0)
        float noOvershoot = MaxSpeedWithoutOvershoot(animProgress);
        if (_currentSpeed > noOvershoot)
        {
            _currentSpeed = noOvershoot;
            _speedVelocity = 0f;
        }

        _animator.speed = _currentSpeed;
    }

    private bool IsPlayFullFinished()
    {
        if (_animator.IsInTransition(_layerIndex)) return false;

        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(_layerIndex);

        // animator ออกจาก state นั้นไปเองแล้ว (เช่นมี exit transition) ถือว่าจบ
        if (info.shortNameHash != _stateHash && info.fullPathHash != _stateHash) return true;

        return info.normalizedTime >= 1f;
    }

    // คืน animator ให้เล่นด้วย speed ปกติ ระหว่างที่ driver ไม่ได้คุม
    private void PassThrough()
    {
        _currentSpeed = 1f;
        _speedVelocity = 0f;
        _animator.speed = 1f;

    }

    private float CalculatSpeed(float animProgress)
    {
        float gap = _progress - animProgress;
        if (gap <= arriveThreshold) return 0f; // forward only, never rewind

        // Normalized time per second needed to close the gap in catchUpTime,
        // converted to Animator.speed using the state length.
        float neededRate = gap / catchUpTime;
        float speed = neededRate * _stateLength * speedMultiple;
        return Mathf.Min(speed, maxSpeed);
    }

    private float MaxSpeedWithoutOvershoot(float animProgress)
    {
        float gap = _progress - animProgress;
        if (gap <= 0f) return 0f;

        float dt = _animator.updateMode == AnimatorUpdateMode.UnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
        if (dt <= 0f) return maxSpeed;

        return gap * _stateLength / dt;
    }

    private void CacheStateLength(AnimatorStateInfo info)
    {
        // length can be Infinity when the state speed is 0, keep the last valid value
        if (!float.IsInfinity(info.length) && info.length > 0.0001f)
            _stateLength = info.length;
    }

    private void ReleaseSpeed()
    {
        _currentSpeed = 1;

        if (Mathf.Abs(_currentSpeed - 1f) < 0.01f)
        {
            _currentSpeed = 1f;
            _speedVelocity = 0f;
            _releasing = false;
        }

        _animator.speed = _currentSpeed;
        StartCoroutine(ReleaseDelayRoutine());
    }

    private float Smooth(float current, float target, float smoothTime)
    {
        if (smoothTime <= 0f)
        {
            _speedVelocity = 0f;
            return target;
        }
        return Mathf.SmoothDamp(current, target, ref _speedVelocity, smoothTime);
    }

    private void HandleSetEnable(bool enable)
    {
        _active = enable;

        if (_animator == null) return;

        if (_active)
        {
            _releasing = false;
            _currentSpeed = _animator.speed;
        }
        else
        {
            // ค่อยๆ คืน speed กลับเป็น 1 แทนการตัดทันที
            _releasing = true;
        }
    }

    private void HandleSetProgress(
       string stateName,
       int layerIndex,
       float progress,
       float fadeDuration,
       AnimatorUpdateMode updateMode = AnimatorUpdateMode.Normal)
    {
        if (!_active && !_isCurrentStateValid) return;

        int hash = Animator.StringToHash(stateName);

        // ยังเล่น PlayFull ไม่จบ เก็บคำสั่งล่าสุดไว้ก่อน
        if (_playingFull && hash != _stateHash)
        {
            _hasPending = true;
            _pendingStateName = stateName;
            _pendingLayerIndex = layerIndex;
            _pendingProgress = progress;
            _pendingFadeDuration = fadeDuration;
            _pendingUpdateMode = updateMode;
            return;
        }

        _animator.updateMode = updateMode;

        if (!_animator.HasState(layerIndex, hash))
        {
            Debug.LogWarning(
                $"Animation state '{stateName}' was not found.",
                this
            );

            HandleSetEnable(false);

            return;
        }

        

        _layerIndex = layerIndex;
        _stateHash = hash;

        if (progress == PlayFull)
        {
            _playingFull = true;
            _animator.CrossFade(stateName, fadeDuration, layerIndex, 0f);
            _animator.Update(0);
            return;
        }

        stateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        bool alreadyTarget = stateInfo.IsName(stateName);

        // ถ้ากำลัง CrossFade ไปหา state นี้อยู่แล้ว ไม่ต้องสั่ง CrossFade ซ้ำ  
        if (!alreadyTarget && _animator.IsInTransition(layerIndex))
        {
            alreadyTarget = _animator.GetNextAnimatorStateInfo(layerIndex).IsName(stateName);
        }

        if (alreadyTarget)
        {
            _progress = progress;
        }
        else
        {
            _animator.CrossFade(stateName, fadeDuration, layerIndex);
            _animator.Update(0);
            // ไม่ตั้ง speed = 0 ทันที ปล่อยให้ SmoothDamp ค่อยๆ ลด/เพิ่มเอง  
            _progress = progress;
        }
    }

    //นับเวลาปรับ animator updateMode กลับเป็น Normal หลังปิด driver
    private IEnumerator ReleaseDelayRoutine()
    {
        yield return new WaitForSeconds(releaseDelay);
        _animator.updateMode = AnimatorUpdateMode.Normal;
    }
}
