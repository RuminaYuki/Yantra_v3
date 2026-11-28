// SequencePlayer.cs : เล่น step ไล่ตามลำดับ
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Sequence : MonoBehaviour
{
    [SerializeField] private bool playOneTime = true;  // ถ้า true จะเล่นครั้งเดียวแล้ว disable gameObject นี้
    [SerializeField] private List<SequenceStep> steps = new();
    [SerializeField] private UnityEvent onStarted;   // เช่น ปิด input/กล้องผู้เล่น
    [SerializeField] private UnityEvent onFinished;  // เช่น เปิดกลับ

    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public event Action Finished;

    bool hasPlayed;

    Coroutine routine;

    [ContextMenu("Play Sequence")]
    public void Play()
    {
        if (IsPlaying || hasPlayed) return;
        if (playOneTime) hasPlayed = true;
        IsPaused = false;
        routine = StartCoroutine(RunAll());
    }

    public void Pause()
    {
        if (!IsPlaying || IsPaused) return;

        IsPaused = true;
        SetStepsPaused(true);
    }

    public void Resume()
    {
        if (!IsPlaying || !IsPaused) return;

        IsPaused = false;
        SetStepsPaused(false);
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
        {
            while (IsPaused)
                yield return null;

            step.SetSequencePaused(false);
            yield return step.Run();
        }
        End();
    }

    void SetStepsPaused(bool paused)
    {
        foreach (var step in steps)
            if (step != null) step.SetSequencePaused(paused);
    }

    void End()
    {
        foreach (var step in steps)
        {
            if (step == null) continue;
            step.SetSequencePaused(false);
            step.OnSequenceEnd();
        }

        routine = null;
        IsPaused = false;
        IsPlaying = false;
        onFinished?.Invoke();
        Finished?.Invoke();
    }
}