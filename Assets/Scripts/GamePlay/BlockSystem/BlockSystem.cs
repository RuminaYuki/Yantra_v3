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
    [SerializeField] float _currentGuardPoints = 100f;
    [SerializeField] float _guardDecreasedPerHit = 10f;
    [SerializeField] VoidEventChannelSO _voidHitEventChannel;
    [SerializeField] VoidEventChannelSO _OnGuardPointDepleted;
    [SerializeField] FloatEventChannelSO _OnCurrentGuardPointChange;

    [Header("Parry System")]
    [SerializeField] bool _isParryEnable = false;
    [SerializeField] float _skillPointPerHit = 5f;

    private void Awake()
    {
        if (_health == null) _health = GetComponent<Health>();
        if (_skillPoints == null) _skillPoints = GetComponent<SkillPoints>();

        _currentGuardPoints = _maxGuardPoints;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            ExcuteBlock(!_isBlockEnable);
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            ExcuteParry(!_isParryEnable);
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

    public void ExcuteParry(bool isParry)
    {
        _health.SetEnableIgnoreDamage(isParry);
        _isParryEnable = isParry;
    }
    public void ExcuteBlock(bool isBlock)
    {
        _health.SetEnableIgnoreDamage(isBlock);
        _isBlockEnable = isBlock;
        if (!isBlock)
        {
            _currentGuardPoints = _maxGuardPoints;
        }
    }
}
