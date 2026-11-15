using System.Collections;
using UnityEngine;

public class WaitStep : SequenceStep
{
    [SerializeField] float seconds = 1f;
    public override IEnumerator Run() { yield return new WaitForSeconds(seconds); }
}