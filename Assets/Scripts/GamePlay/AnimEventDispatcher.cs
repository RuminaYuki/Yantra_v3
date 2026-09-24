using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] // Mark the class as serializable
public class SerializableAction
{
    public event Action ActionEvent;

    public void Invoke() => ActionEvent?.Invoke();
    public void AddListener(Action listener)
    {
        ActionEvent -= listener;
        ActionEvent += listener;
    }
    public void RemoveListener(Action listener) => ActionEvent -= listener;
    public int ListenerCount => ActionEvent?.GetInvocationList().Length ?? 0;
}

public class AnimEventDispatcher : MonoBehaviour
{
    [SerializeField]
    private Dictionary<string, SerializableAction> events = new();

    public void OnSpawnEvent(string key)
    {
        if (events.TryGetValue(key, out var evt))
        {
            evt.Invoke();
        }
        else
            Debug.LogWarning($"[AnimEventDispatcher] ไม่มีใครซับ key: {key}");
    }

    // ส่ง key เข้ามา → ได้ event ตัวจริงกลับไป ถ้ายังไม่มี key นี้ก็สร้างให้ใหม่
    public SerializableAction GetEvent(string key)
    {
        if (!events.TryGetValue(key, out var evt))
        {
            evt = new SerializableAction();
            events[key] = evt;
        }
        return evt;
    }
}
