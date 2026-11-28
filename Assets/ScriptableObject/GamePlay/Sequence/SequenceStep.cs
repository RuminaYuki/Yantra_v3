using System.Collections;
using UnityEngine;

public abstract class SequenceStep : MonoBehaviour
{
    public bool IsSequencePaused { get; private set; }

    public abstract IEnumerator Run();

    internal void SetSequencePaused(bool paused) => IsSequencePaused = paused;

    protected IEnumerator WaitWhileSequencePaused()
    {
        while (IsSequencePaused)
            yield return null;
    }

    // sequence จบหรือถูก Stop แล้ว ให้ step เก็บกวาด/คืนค่าที่ค้างไว้
    public virtual void OnSequenceEnd() { }
}