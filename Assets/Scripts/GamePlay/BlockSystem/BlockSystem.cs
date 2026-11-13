using Unity.Collections;
using UnityEngine;
using static Unity.Collections.AllocatorManager;
using static UnityEngine.Rendering.DebugUI;

public class BlockSystem : MonoBehaviour
{
    [Header("Refeerences")]
    [SerializeField] Health _health;
    [SerializeField] SkillPoints _skillPoints;

    [Header("Block System")]
    [SerializeField] bool _isBlockEnable = false;
    [SerializeField] float _maxGuardPoints = 100f;
    [SerializeField, Range(0f, 100f)] float _minGuardPercent = 0f;   // 0 = ปิด
    [Tooltip("พอ guard ลดถึง floor จะหยุดลดต่อวินาทีไว้กี่วินาที (hit ยังลดได้ตามปกติ)")]
    [SerializeField, Min(0f)] float _floorHoldDuration = 1f;
    [SerializeField] bool _assignValue = false;
    [SerializeField] float _currentGuardPoints = 100f;
    [SerializeField] float _guardDecreasedPerHit = 10f;
    [SerializeField] float _recoveryGuardPointsPerSecond = 5f;
    [SerializeField] float _decreasedGuardPointsPerSecond = 5f;
    [SerializeField] float _delayForRecovery = 1f;
    [SerializeField] VoidEventChannelSO _voidHitEventChannel;
    [SerializeField] VoidEventChannelSO _OnGuardPointDepleted;
    [SerializeField] FloatEventChannelSO _OnCurrentGuardPointChange;

    [Header("Parry System")]
    [SerializeField] bool _isParryEnable = false;
    [SerializeField] float _skillPointPerHit = 5f;
    [SerializeField] float _guardDecreasedPerParry = 5f;

    private float _recoveryDelayTimer;
    private bool _isReady = true;

    private float MinGuard => _maxGuardPoints * _minGuardPercent / 100f;
    public float MinGuardPercent => _minGuardPercent;

    private float _floorHoldTimer;
    // true ระหว่างค้างที่ floor (หยุดลดต่อวินาที)
    public bool IsFloorHolding => _floorHoldTimer > 0f;

    public void SetMinGuardPercent(float percent)
    {
        _minGuardPercent = Mathf.Clamp(percent, 0f, 100f);
    }

    public float CurrentGuardPoints => _currentGuardPoints;
    // false ตั้งแต่ guard ลงถึง 0 จนกว่าจะฟื้นเต็ม
    public bool IsReady => _isReady;
    public bool IsParryEnable => _isParryEnable;
    public float MaxGuardPoints
    {
        get => _maxGuardPoints;
        set
        {
            if (value <= 0)
            {
                Debug.LogWarning("Max guard points must be greater than zero.");
                return;
            }
            _maxGuardPoints = value;
            if (_currentGuardPoints > _maxGuardPoints)
            {
                SetGuard(_maxGuardPoints);
                _OnCurrentGuardPointChange.Raise(_currentGuardPoints);
            }
        }
    }

    private void Awake()
    {
        if (_health == null) _health = GetComponent<Health>();
        if (_skillPoints == null) _skillPoints = GetComponent<SkillPoints>();

        if(_assignValue)
            SetGuard(_maxGuardPoints);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            ExecuteBlock(!_isBlockEnable);
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            ExecuteParry(!_isParryEnable);
        }

        if (_isBlockEnable)
        {
            _recoveryDelayTimer = 0f;

            if (_floorHoldTimer > 0f)
                _floorHoldTimer -= Time.deltaTime;   // ค้างที่ floor อยู่ ยังไม่ลด
            else
                SetGuard(ApplyFloor(_decreasedGuardPointsPerSecond * Time.deltaTime));

            if (_currentGuardPoints <= 0)
            {
                _OnGuardPointDepleted.Raise();
            }
        }

        if (_currentGuardPoints < _maxGuardPoints)
        {
            _recoveryDelayTimer += Time.deltaTime;
            if (_recoveryDelayTimer >= _delayForRecovery)
            {
                SetGuard(_currentGuardPoints + _recoveryGuardPointsPerSecond * Time.deltaTime);
            }
        }
    }

    private void OnEnable()
    {
        if (_voidHitEventChannel != null)
            _voidHitEventChannel.Raised += OnHit;
    }

    private void OnDisable()
    {
        if (_voidHitEventChannel != null)
            _voidHitEventChannel.Raised -= OnHit;
    }

    private void OnHit()
    {
        if (_skillPoints != null && _isParryEnable)
        {
            _skillPoints.gaint(_skillPointPerHit);
            DecreaseGuard(_guardDecreasedPerParry);
            return;
        }

        if (!_isBlockEnable) return;

        DecreaseGuard(_guardDecreasedPerHit);
    }

    private void DecreaseGuard(float amount)
    {
        _recoveryDelayTimer = 0f;

        SetGuard(ApplyFloor(amount));
        _OnCurrentGuardPointChange.Raise(_currentGuardPoints);
        if (_currentGuardPoints <= 0)
        {
            _OnGuardPointDepleted.Raise();
        }
    }

    // ยังอยู่เหนือ floor → ลดได้แค่ถึง floor แล้วเริ่มค้าง / ถึง floor แล้ว → ลดตามปกติ (เหมือน Health)
    private float ApplyFloor(float amount)
    {
        float next = _currentGuardPoints - amount;

        if (MinGuard > 0f && _currentGuardPoints > MinGuard && next <= MinGuard)
        {
            _floorHoldTimer = _floorHoldDuration;
            return MinGuard;
        }

        return next;
    }

    // ทุกที่ที่เปลี่ยนค่า guard ต้องผ่านตรงนี้ เพื่อให้ IsReady อัปเดตที่เดียว
    private void SetGuard(float value)
    {
        _currentGuardPoints = Mathf.Clamp(value, 0f, _maxGuardPoints);

        if (_currentGuardPoints <= 0f)
            _isReady = false;
        else if (_currentGuardPoints >= _maxGuardPoints)
            _isReady = true;
    }

    public void ExecuteParry(bool isParry)
    {
        _health.SetEnableIgnoreDamage(isParry);
        _isParryEnable = isParry;
    }
    public void ExecuteBlock(bool isBlock)
    {
        _health.SetEnableIgnoreDamage(isBlock);
        _isBlockEnable = isBlock;
    }
}
