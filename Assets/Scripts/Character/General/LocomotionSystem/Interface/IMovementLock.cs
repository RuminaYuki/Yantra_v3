using UnityEngine;

public interface ILocomotionLock
{
    bool IsMovementLocked { get; }
    bool ShouldResetMoveAnimation { get; }

    void LockLocomotion(object owner, bool resetMoveAnimation = true);
    void UnlockLocomotion(object owner);
}
