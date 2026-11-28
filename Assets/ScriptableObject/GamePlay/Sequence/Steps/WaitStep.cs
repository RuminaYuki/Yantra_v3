using System.Collections;
using UnityEngine;

public class WaitStep : SequenceStep
{
    [SerializeField] float seconds = 1f;

    public override IEnumerator Run()
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return WaitWhileSequencePaused();
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}