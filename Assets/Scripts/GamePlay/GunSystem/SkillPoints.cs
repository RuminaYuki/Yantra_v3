using UnityEngine;

public class SkillPoints : MonoBehaviour
{
    [SerializeField] float _skillPoints = 0f;

    public float CurrentSkillPoints => _skillPoints;

    public void gaint(float amount)
    {
        amount = amount < 0 ? -amount : amount;
        _skillPoints += amount;
    }

    public bool consume(float amount)
    {
        amount = amount < 0 ? -amount : amount;
        if (_skillPoints >= amount)
        {
            _skillPoints -= amount;
            return true;
        }
        return false;
    }
}
