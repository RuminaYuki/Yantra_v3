using Unity.Collections;
using UnityEngine;

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

    private void Awake()
    {
        if (_health == null) _health = GetComponent<Health>();
        if (_skillPoints == null) _skillPoints = GetComponent<SkillPoints>();

        _currentGuardPoints = _maxGuardPoints;
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
        _currentGuardPoints = Mathf.Max(0, _currentGuardPoints - _guardDecreasedPerHit);
        if (_currentGuardPoints <= 0)
        {
            _isBlockEnable = false;
            _health.SetEnableIgnoreDamage(false);
        }
    }

    public void ExcuteParry(bool isParry)
    {
        _health.SetEnableIgnoreDamage(isParry);
    }
    public void ExcuteBlock(bool isBlock)
    {
        _health.SetEnableIgnoreDamage(isBlock);
    }

}
