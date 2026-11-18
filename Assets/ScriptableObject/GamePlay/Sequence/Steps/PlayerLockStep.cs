using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLockStep : SequenceStep
{
    [SerializeField] MonoBehaviour lockSource;
    [SerializeField] PlayerLocomotion playerLocomotion;
    [SerializeField] bool isLocked = true;
    [SerializeField] InputActionReference[] actionsToLock;

    private readonly Dictionary<InputAction, bool> _actionEnabledStates = new();
    private bool _actionsLocked;
    private bool _ownsLocomotionLock;

    public override IEnumerator Run()
    {   
        object owner = lockSource != null ? lockSource : this;
        if (isLocked)
        {
            playerLocomotion.LockLocomotion(owner);
            _ownsLocomotionLock = true;
            SetActionsLocked(true);
        }
        else
        {
            playerLocomotion.UnlockLocomotion(owner);
            _ownsLocomotionLock = false;
            SetActionsLocked(false);
        }
        yield break;
    }

    public override void OnSequenceEnd()
    {
        if (_ownsLocomotionLock)
        {
            object owner = lockSource != null ? lockSource : this;
            playerLocomotion.UnlockLocomotion(owner);
            _ownsLocomotionLock = false;
        }

        SetActionsLocked(false);
    }

    private void SetActionsLocked(bool locked)
    {
        if (_actionsLocked == locked)
            return;

        if (locked)
        {
            _actionEnabledStates.Clear();
            if (actionsToLock != null)
            {
                foreach (InputActionReference actionReference in actionsToLock)
                {
                    InputAction action = actionReference != null ? actionReference.action : null;
                    if (action == null || _actionEnabledStates.ContainsKey(action))
                        continue;

                    bool wasEnabled = action.enabled;
                    _actionEnabledStates[action] = wasEnabled;
                    if (wasEnabled)
                        action.Disable();
                }
            }

            _actionsLocked = true;
            return;
        }

        foreach (KeyValuePair<InputAction, bool> actionState in _actionEnabledStates)
        {
            if (actionState.Value && !actionState.Key.enabled)
                actionState.Key.Enable();
        }

        _actionEnabledStates.Clear();
        _actionsLocked = false;
    }
}
