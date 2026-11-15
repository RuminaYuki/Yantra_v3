using UnityEngine.Events;
using UnityEngine;
using System.Collections;

public class EventStep : SequenceStep   // เรียกอะไรก็ได้ เช่น เปิดประตู, เล่นเสียง, โชว์ UI
{
    [SerializeField] UnityEvent action;
    public override IEnumerator Run() { action?.Invoke(); yield break; }
}