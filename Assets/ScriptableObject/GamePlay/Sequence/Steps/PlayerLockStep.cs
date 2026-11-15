using System.Collections;
using UnityEngine;

public class PlayerLockStep : SequenceStep
{
    [SerializeField] MonoBehaviour lockSource;
    [SerializeField] PlayerLocomotion playerLocomotion;
    [SerializeField] bool isLocked = true;

    public override IEnumerator Run()
    {   
        switch (isLocked)
        {
            case true:
                playerLocomotion.LockLocomotion(lockSource);
                break;
            case false:
                playerLocomotion.UnlockLocomotion(lockSource);
                break;
        }
        yield break;
    }
}
