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

    private float _recoveryDelayTimer;

    public float CurrentGuardPoints => _currentGuardPoints;
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
                _currentGuardPoints = _maxGuardPoints;
                _OnCurrentGuardPointChange.Raise(_currentGuardPoints);
            }
        }
    }

    private void Awake()
    {
        if (_health == null) _health = GetComponent<Health>();
        if (_skillPoints == null) _skillPoints = GetComponent<SkillPoints>();

        if(_assignValue)
            _currentGuardPoints = _maxGuardPoints;
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
            _currentGuardPoints = Mathf.Max(0, _currentGuardPoints - _decreasedGuardPointsPerSecond * Time.deltaTime);
            if (_currentGuardPoints <= 0)
            {
                _OnGuardPointDepleted.Raise();
            }
        }

        if (!_isBlockEnable && _currentGuardPoints < _maxGuardPoints)
        {
            _recoveryDelayTimer += Time.deltaTime;
            if (_recoveryDelayTimer >= _delayForRecovery)
            {
                _currentGuardPoints = Mathf.Min(_maxGuardPoints, _currentGuardPoints + _recoveryGuardPointsPerSecond * Time.deltaTime);
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
            return;
        }

        if (!_isBlockEnable) return;

        _currentGuardPoints = Mathf.Max(0, _currentGuardPoints - _guardDecreasedPerHit);
        _OnCurrentGuardPointChange.Raise(_currentGuardPoints);
        if (_currentGuardPoints <= 0)
        {
            _OnGuardPointDepleted.Raise();
        }
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
