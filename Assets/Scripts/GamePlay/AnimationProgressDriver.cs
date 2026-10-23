using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationProgressDriver : MonoBehaviour
{

    [SerializeField] Animator _animator;
    [SerializeField] BoolEventChannelSO _aPD_SetEnable_AEC;
    [SerializeField] AnimationProgressDriverAdvEventChannal _aPD_AEC;

    [Header("Setting")]
    [SerializeField] float speedMultiple = 1f;

    [Header("Smoothing")]
    [Tooltip("เวลาที่ใช้ไล่ speed ไปหาค่าเป้าหมาย (ยิ่งมากยิ่งนุ่ม แต่ตอบสนองช้าลง)")]
    [SerializeField, Min(0f)] float speedSmoothTime = 0.1f;
    [Tooltip("จำกัด speed สูงสุด กันกระชากเวลา progress กระโดดไกล")]
    [SerializeField, Min(0f)] float maxSpeed = 3f;
    [Tooltip("เวลาที่ใช้คืน speed กลับเป็น 1 หลังปิด driver")]
    [SerializeField, Min(0f)] float releaseSmoothTime = 0.15f;

    private int _layerIndex;
    private int _stateHash;
    private bool _active = false;
    private bool _releasing = false;
    private float _progress = 0;
    private float _currentSpeed = 1f;
    private float _speedVelocity = 0f;
    AnimatorStateInfo stateInfo;

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
    }

    private void Update()
    {
        if (_active)
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
        float targetSpeed = CalculatSpeed(animProgress);

        _currentSpeed = Smooth(_currentSpeed, targetSpeed, speedSmoothTime);

        animProgress = stateInfo.normalizedTime;
        if (animProgress > _progress) _currentSpeed = 0f;

        Debug.Log($"AnimationProgressDriver | animProgress={animProgress} | targetSpeed={targetSpeed} | _currentSpeed={_currentSpeed}");
        _animator.speed = _currentSpeed;
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
        float diff = _progress - animProgress;
        float speed = Mathf.Max(diff, 0f) * 10f * speedMultiple; // เดินไปข้างหน้าอย่างเดียว ไม่ถอย
        return Mathf.Min(speed, maxSpeed);
    }

    private void ReleaseSpeed()
    {
        _currentSpeed = Smooth(_currentSpeed, 1f, releaseSmoothTime);

        if (Mathf.Abs(_currentSpeed - 1f) < 0.01f)
        {
            _currentSpeed = 1f;
            _speedVelocity = 0f;
            _releasing = false;
        }

        _animator.speed = _currentSpeed;
        _animator.updateMode = AnimatorUpdateMode.Normal;
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
        if (!_active) return;

        _animator.updateMode = updateMode; // Explicit cast added here  

        int hash = Animator.StringToHash(stateName);

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
}
