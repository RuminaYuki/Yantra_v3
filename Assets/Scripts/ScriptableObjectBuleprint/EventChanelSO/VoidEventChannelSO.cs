using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "VoidEventChannel",
    menuName = "Event Channel/Void Event")]
public class VoidEventChannelSO : ScriptableObject
{
    public event Action Raised;

    public void Raise()
    {
        Raised?.Invoke();
    }
}