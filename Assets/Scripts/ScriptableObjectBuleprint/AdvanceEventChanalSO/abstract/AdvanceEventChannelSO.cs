using System;
using UnityEngine;

public abstract class AdvanceEventChannelSO<T1, T2> : ScriptableObject
{
    public event Action<T1, T2> Raised;

    public void Raise(T1 value1, T2 value2 = default)
    {
        Raised?.Invoke(value1, value2);
    }
}
public abstract class AdvanceEventChannelSO<T1, T2, T3> : ScriptableObject
{
    public event Action<T1, T2, T3> Raised;

    public void Raise(T1 value1, T2 value2 = default, T3 value3 = default)
    {
        Raised?.Invoke(value1, value2, value3);
    }
}
public abstract class AdvanceEventChannelSO<T1, T2, T3, T4> : ScriptableObject
{
    public event Action<T1, T2, T3, T4> Raised;

    public void Raise(T1 value1, T2 value2 = default, T3 value3 = default, T4 value4 = default)
    {
        Raised?.Invoke(value1, value2, value3, value4);
    }
}

public abstract class AdvanceEventChannelSO<T1, T2, T3, T4, T5> : ScriptableObject
{
    public event Action<T1, T2, T3, T4, T5> Raised;

    public void Raise(T1 value1, T2 value2 = default, T3 value3 = default, T4 value4 = default, T5 value5 = default)
    {
        Raised?.Invoke(value1, value2, value3, value4, value5);
    }
}