using System.Collections.Generic;
using UnityEngine;

// Weighted random picks per pool, skipping options still on cooldown, so a state machine can roll
// once on enter (Roll Random Pool) and let each transition just check the result (Random Picked).
// Picking an option starts its cooldown, so the states after it don't need a StartCooldown action.
// CooldownController is optional: without one, every option counts as ready.
public class RandomPickController : MonoBehaviour
{
    [System.Serializable]
    private struct Entry
    {
        public RandomOptionSO Option;
        [Tooltip("Share against the other ready options, not a percent: 1, 1, 1, 1 is 25% each. 0 never picks it.")]
        [Range(0f, 1f)] public float Weight;
        [Tooltip("Empty = no cooldown. Several options can share one, so picking either blocks both.")]
        public CooldownSO Cooldown;
        [Tooltip("Seconds before it can be picked again, counted from when it's picked.")]
        [Min(0f)] public float CooldownDuration;
    }

    [System.Serializable]
    private class Pool
    {
        public RandomPoolSO Id;
        public List<Entry> Options = new();
    }

    [SerializeField] private List<Pool> _pools = new();

    private CooldownController _cooldowns;
    private readonly List<Entry> _ready = new();

    // RandomPoolSO -> its last pick. Missing or null: never rolled, or nothing was ready.
    private readonly Dictionary<RandomPoolSO, RandomOptionSO> _current = new();

    private void Awake()
    {
        if (TryGetComponent(out _cooldowns))
        {
            return;
        }

        foreach (Pool pool in _pools)
        {
            foreach (Entry entry in pool.Options)
            {
                if (entry.Cooldown != null)
                {
                    Debug.LogWarning($"Option {(entry.Option != null ? entry.Option.name : "None")} has a cooldown but there's no CooldownController, so it's ignored.", this);
                }
            }
        }
    }

    public RandomOptionSO GetCurrent(RandomPoolSO pool)
    {
        return pool != null && _current.TryGetValue(pool, out RandomOptionSO option) ? option : null;
    }

    // Sets the pool's current pick without rolling and without starting any cooldown.
    // Used to overwrite an old result, e.g. after leaving Glory so "Random Picked" checks see the new value.
    public void SetCurrent(RandomPoolSO pool, RandomOptionSO option)
    {
        if (pool == null)
        {
            return;
        }

        _current[pool] = option;
    }

    public RandomOptionSO Roll(RandomPoolSO pool)
    {
        Pool found = FindPool(pool);
        if (found == null)
        {
            Debug.LogWarning($"No pool {(pool != null ? pool.name : "None")} to roll.", this);
            return null;
        }

        _ready.Clear();
        float totalWeight = 0f;

        foreach (Entry entry in found.Options)
        {
            if (entry.Option != null && entry.Weight > 0f && IsReady(entry))
            {
                _ready.Add(entry);
                totalWeight += entry.Weight;
            }
        }

        _current[pool] = null;
        if (_ready.Count == 0)
        {
            return null;
        }

        // Weighted pick: a point on the summed weights, then walk until it falls inside one.
        float point = Random.Range(0f, totalWeight);
        foreach (Entry entry in _ready)
        {
            point -= entry.Weight;
            if (point <= 0f)
            {
                return Pick(pool, entry);
            }
        }

        // Float rounding can leave a tiny remainder.
        return Pick(pool, _ready[^1]);
    }

    private Pool FindPool(RandomPoolSO pool)
    {
        if (pool == null)
        {
            return null;
        }

        foreach (Pool candidate in _pools)
        {
            if (candidate.Id == pool)
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsReady(Entry entry)
    {
        return entry.Cooldown == null || _cooldowns == null || _cooldowns.IsReady(entry.Cooldown);
    }

    private RandomOptionSO Pick(RandomPoolSO pool, Entry entry)
    {
        _current[pool] = entry.Option;

        if (entry.Cooldown != null && _cooldowns != null)
        {
            _cooldowns.StartCooldown(entry.Cooldown, entry.CooldownDuration);
        }

        return entry.Option;
    }
}
