using System.Collections;
using System.Collections.Generic;
using SDFcl.GamePlay.Interactable;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractControlLockAddition : MonoBehaviour
{
    [Header("Controls to lock")]
    [Tooltip("Disable player camera look while the interaction is active.")]
    [SerializeField] private bool _lockCameraLook;
    [Tooltip("Prevent player locomotion while the interaction is active.")]
    [SerializeField] private bool _lockLocomotion;
    [Tooltip("Input actions to disable temporarily. Leave the interact action enabled if it is used to cancel this interaction.")]
    [SerializeField] private InputActionReference[] _blockedActions = System.Array.Empty<InputActionReference>();

    [Header("Release")]
    [Tooltip("0 = hold until the interaction ends. Set a duration for one-shot interactions that do not send OnEndInteract.")]
    [SerializeField, Min(0f)] private float _releaseAfterSeconds;

    private Iinteractor _interactor;
    private GameObject _activeRootPlayer;
    private ILocomotionLock _locomotionLock;
    private PlayerCameraController _cameraController;
    private bool _previousLookLocked;
    private bool _savedLookLock;
    private Coroutine _releaseRoutine;
    private readonly HashSet<InputAction> _actionsToReenable = new();

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
        if (_interactor == null)
        {
            Debug.LogError($"{nameof(InteractControlLockAddition)} requires an interactor on {name}.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_interactor == null) return;

        _interactor.OnInteract += HandleInteract;
        _interactor.OnEndInteract += HandleEndInteract;
    }

    private void OnDisable()
    {
        if (_interactor != null)
        {
            _interactor.OnInteract -= HandleInteract;
            _interactor.OnEndInteract -= HandleEndInteract;
        }

        ReleaseLocks();
    }

    private void HandleInteract(GameObject rootPlayer)
    {
        if (_activeRootPlayer != null) return;
        if (rootPlayer == null)
        {
            Debug.LogWarning($"{nameof(InteractControlLockAddition)} received an interaction without a player.", this);
            return;
        }

        _activeRootPlayer = rootPlayer;

        if (_lockLocomotion)
        {
            _locomotionLock = rootPlayer.GetComponentInChildren<ILocomotionLock>();
            if (_locomotionLock == null)
                Debug.LogWarning($"{nameof(InteractControlLockAddition)}: no {nameof(ILocomotionLock)} found on {rootPlayer.name}.", this);
            else
                _locomotionLock.LockLocomotion(this);
        }

        if (_lockCameraLook)
        {
            _cameraController = rootPlayer.GetComponentInChildren<PlayerCameraController>();
            if (_cameraController == null && Camera.main != null)
                _cameraController = Camera.main.GetComponent<PlayerCameraController>();

            if (_cameraController == null)
                Debug.LogWarning($"{nameof(InteractControlLockAddition)}: no {nameof(PlayerCameraController)} found for {rootPlayer.name}.", this);
            else
            {
                _previousLookLocked = _cameraController.IsLookLocked;
                _savedLookLock = true;
                _cameraController.IsLookLocked = true;
            }
        }

        if (_blockedActions != null)
        {
            foreach (InputActionReference actionReference in _blockedActions)
            {
                InputAction action = actionReference != null ? actionReference.action : null;
                if (action == null || !action.enabled) continue;

                action.Disable();
                _actionsToReenable.Add(action);
            }
        }

        if (_releaseAfterSeconds > 0f)
            _releaseRoutine = StartCoroutine(ReleaseAfterDelay());
    }

    private void HandleEndInteract(GameObject _)
    {
        ReleaseLocks();
    }

    private IEnumerator ReleaseAfterDelay()
    {
        yield return new WaitForSecondsRealtime(_releaseAfterSeconds);
        _releaseRoutine = null;
        ReleaseLocks();
    }

    private void ReleaseLocks()
    {
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }

        if (_locomotionLock != null)
            _locomotionLock.UnlockLocomotion(this);
        _locomotionLock = null;

        if (_savedLookLock && _cameraController != null)
            _cameraController.IsLookLocked = _previousLookLocked;
        _cameraController = null;
        _savedLookLock = false;

        foreach (InputAction action in _actionsToReenable)
            action.Enable();
        _actionsToReenable.Clear();
        _activeRootPlayer = null;
    }
}
