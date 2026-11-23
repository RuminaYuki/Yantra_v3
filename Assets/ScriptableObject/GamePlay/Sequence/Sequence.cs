// SequencePlayer.cs : เล่น step ไล่ตามลำดับ
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Sequence : MonoBehaviour
{
    [SerializeField] private List<SequenceStep> steps = new();
    [SerializeField] private UnityEvent onStarted;   // เช่น ปิด input/กล้องผู้เล่น
    [SerializeField] private UnityEvent onFinished;  // เช่น เปิดกลับ

    public bool IsPlaying { get; private set; }
    public event Action Finished;

    Coroutine routine;

    [ContextMenu("Play Sequence")]
    public void Play()
    {
        if (IsPlaying) return;
        routine = StartCoroutine(RunAll());
    }

    public void Stop()
    {
        if (routine != null) StopCoroutine(routine);
        End();
    }

    IEnumerator RunAll()
    {
        IsPlaying = true;
        onStarted?.Invoke();
        foreach (var step in steps)
            yield return step.Run();
        End();
    }

    void End()
    {
        foreach (var step in steps)
            if (step != null) step.OnSequenceEnd();

        IsPlaying = false;
        onFinished?.Invoke();
        Finished?.Invoke();
    }
}