using UnityEngine;
using System.Collections.Generic;

public class AttackCoordinator : MonoBehaviour
{
    [SerializeField, Min(1)] private int _maxAttackers = 1;
    private readonly List<GameObject> _currentAttackers = new List<GameObject>();

    public int MaxAttackers
    {
        get => _maxAttackers;
        set
        {
            _maxAttackers = Mathf.Max(1, value);

            while (_currentAttackers.Count > _maxAttackers)
            {
                _currentAttackers.RemoveAt(_currentAttackers.Count - 1);
            }
        }
    }

    public int CurrentAttackerCount => _currentAttackers.Count;

    public bool TryClaim(GameObject requester)
    {
        if (requester == null)
            return false;

        if (_currentAttackers.Contains(requester))
            return true;

        if (_currentAttackers.Count >= _maxAttackers)
            return false;

        _currentAttackers.Add(requester);
        return true;
    }

    public bool IsOwner(GameObject requester)
    {
        return requester != null &&
               _currentAttackers.Contains(requester);
    }

    public void Release(GameObject requester)
    {
        if (requester != null)
        {
            _currentAttackers.Remove(requester);
        }
    }
}
