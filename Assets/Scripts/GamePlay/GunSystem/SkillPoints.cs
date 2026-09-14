using Unity.Mathematics;
using UnityEngine;

public class SkillPoints : MonoBehaviour
{
    [SerializeField] float _skillPoints = 0f;
    [SerializeField] float _maxPoint = 10f;
    [SerializeField] VoidEventChannelSO _onMaxPoint;

    public float CurrentSkillPoints => _skillPoints;

    public void gaint(float amount)
    {
        amount = amount < 0 ? -amount : amount;
        _skillPoints += amount;
        _skillPoints = Mathf.Clamp(_skillPoints, 0f, _maxPoint);
        if (_onMaxPoint != null )
        {
            if (_skillPoints >=  _maxPoint) _onMaxPoint.Raise();
        }
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

}
