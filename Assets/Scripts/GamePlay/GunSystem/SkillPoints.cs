using Unity.Mathematics;
using UnityEngine;

public class SkillPoints : MonoBehaviour
{
    [Header("Skill Points Settings")]
    [SerializeField] float _skillPoints = 0f;
    [SerializeField] float _maxPoint = 10f;
    [SerializeField] VoidEventChannelSO _onMaxPoint;

    [Header("Decay Settings")]
    [SerializeField] float _decayDelay = 20f;
    [SerializeField] float _decayRate = 1f;
    [SerializeField] float _decayMin = 0f;

    private float _decayTimer = 0f;

    public float CurrentSkillPoints => _skillPoints;
    public float MaxSkillPoints
    {
        get => _maxPoint;
        set
        {
            if (value <= 0)
            {
                Debug.LogWarning("Max skill points must be greater than zero.");
                return;
            }
            _maxPoint = value;
            if (_skillPoints > _maxPoint)
            {
                _skillPoints = _maxPoint;
            }
        }
    }

    private void Update()
    {
        if (_decayTimer <= 0)
        {
            _skillPoints -= _decayRate * Time.deltaTime;
            _skillPoints = Mathf.Clamp(_skillPoints, _decayMin, _maxPoint);
        }
        else
        {
            _decayTimer -= Time.deltaTime;
        }
    }

    // Used by the save system. Clamped to 0..max; does not raise the max-point event.
    public void SetCurrent(float value)
    {
        _skillPoints = Mathf.Clamp(value, 0f, _maxPoint);
    }

    public void gaint(float amount)
    {
        amount = amount < 0 ? -amount : amount;
        _skillPoints += amount;
        _skillPoints = Mathf.Clamp(_skillPoints, 0f, _maxPoint);
        if (_onMaxPoint != null )
        {
            if (_skillPoints >=  _maxPoint) _onMaxPoint.Raise();
        }

        _decayTimer = _decayDelay;
    }

    public bool consume(float amount)
    {
        amount = amount < 0 ? -amount : amount;
        if (_skillPoints >= amount)
        {
            _skillPoints -= amount;
            _skillPoints = Mathf.Clamp(_skillPoints, 0f, _maxPoint);
            return true;
        }
        return false;
    }

    public bool TryConsume(float amount)
    {
        amount = amount < 0 ? -amount : amount;
        float curent = _skillPoints;
        if (curent >= amount)
        {
            curent -= amount;
            curent = Mathf.Clamp(_skillPoints, 0f, _maxPoint);
            return true;
        }
        return false;
    }
}
