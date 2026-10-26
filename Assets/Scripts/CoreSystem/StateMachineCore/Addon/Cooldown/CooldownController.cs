using System.Collections.Generic;
using UnityEngine;
using Yuki.Learning.StateMachine;

// Several cooldowns per character, each by its CooldownSO. A cooldown keeps counting whatever state the
// character is in (it's a time it ends at, not something a state ticks), and needs no registering first.
// Uses scaled time, so pausing the game (timeScale 0) pauses cooldowns too.
// Counts on the owner's StateMachineClock, so a slowed character's cooldowns run slower too.
public class CooldownController : MonoBehaviour
{
    private StateMachineClock _clock;

    private void Awake()
    {
        _clock = new StateMachineClock(gameObject);
    }

    public enum StartMode
    {
        Restart,      // always from the new duration
        KeepLonger    // only if it ends later than what's left
    }

    // CooldownSO -> clock time it ends at.
    private readonly Dictionary<CooldownSO, float> _endTimes = new();

#if UNITY_EDITOR
    // Inspector view only (Play Mode): what's counting and how long is left.
    [System.Serializable]
    private struct DebugEntry
    {
        public CooldownSO Cooldown;
        public float Remaining;
    }

    [SerializeField] private List<DebugEntry> _running = new();

    private void Update()
    {
        _running.Clear();
        foreach (KeyValuePair<CooldownSO, float> pair in _endTimes)
        {
            float remaining = pair.Value - _clock.Now;
            if (remaining > 0f && pair.Key != null)
            {
                _running.Add(new DebugEntry { Cooldown = pair.Key, Remaining = remaining });
            }
        }
    }
#endif

    public void StartCooldown(CooldownSO cooldown, float duration, StartMode mode = StartMode.Restart)
    {
        if (cooldown == null)
        {
            Debug.LogWarning("Cannot start a null cooldown.", this);
            return;
        }

        float endTime = _clock.Now + Mathf.Max(0f, duration);

        if (mode == StartMode.KeepLonger &&
            _endTimes.TryGetValue(cooldown, out float current) &&
            current > endTime)
        {
            return;
        }

        _endTimes[cooldown] = endTime;
    }

    // Never started, or its time is up.
    public bool IsReady(CooldownSO cooldown)
    {
        return GetRemaining(cooldown) <= 0f;
    }

    public float GetRemaining(CooldownSO cooldown)
    {
        if (cooldown == null || !_endTimes.TryGetValue(cooldown, out float endTime))
        {
            return 0f;
        }

        return Mathf.Max(0f, endTime - _clock.Now);
    }

    // Ready right away.
    public void ResetCooldown(CooldownSO cooldown)
    {
        if (cooldown != null)
        {
            _endTimes.Remove(cooldown);
        }
    }

    public void ResetAll()
    {
        _endTimes.Clear();
    }
}
