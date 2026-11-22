using System;
using UnityEngine;
using UnityEngine.Events;

public class OnGameStateChange : MonoBehaviour
{
    [Serializable]
    public class State
    {
        [Tooltip("ชื่อ State ที่ต้องการให้ทำงาน")]
        public string Name;
        public VoidEventChannelSO EventChannel;
        public UnityEvent OnStateChange;
    }

    [SerializeField] private State[] _states;
}
