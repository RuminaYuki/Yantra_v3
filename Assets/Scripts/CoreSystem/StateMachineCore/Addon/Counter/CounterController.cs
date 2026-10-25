using System.Collections.Generic;
using UnityEngine;

// Several int counters per character, each by its CounterSO, for loops in a state machine
// ("shoot 3 times"): Change Counter sets / adds, Counter Compare checks. Needs no registering first;
// a counter never set reads 0.
public class CounterController : MonoBehaviour
{
    private readonly Dictionary<CounterSO, int> _values = new();

#if UNITY_EDITOR
    // Inspector view only (Play Mode): every counter set so far and its value.
    [System.Serializable]
    private struct DebugEntry
    {
        public CounterSO Counter;
        public int Value;
    }

    [SerializeField] private List<DebugEntry> _current = new();

    private void Update()
    {
        _current.Clear();
        foreach (KeyValuePair<CounterSO, int> pair in _values)
        {
            if (pair.Key != null)
            {
                _current.Add(new DebugEntry { Counter = pair.Key, Value = pair.Value });
            }
        }
    }
#endif

    public int Get(CounterSO counter)
    {
        return counter != null && _values.TryGetValue(counter, out int value) ? value : 0;
    }

    public void Set(CounterSO counter, int value)
    {
        if (counter == null)
        {
            Debug.LogWarning("Cannot set a null counter.", this);
            return;
        }

        _values[counter] = value;
    }

    public void Add(CounterSO counter, int amount)
    {
        Set(counter, Get(counter) + amount);
    }

    // Back to 0.
    public void ResetCounter(CounterSO counter)
    {
        if (counter != null)
        {
            _values.Remove(counter);
        }
    }

    public void ResetAll()
    {
        _values.Clear();
    }
}
