using System.Collections;
using SDFcl.GamePlay.Interactable;
using UnityEngine;

public class InteractLookTargetAddition : MonoBehaviour
{
    [Header("Look target")]
    [Tooltip("Place a Transform at the point the camera should look toward.")]
    [SerializeField] private Transform _lookTarget;
    [Tooltip("World-space offset added to the target position.")]
    [SerializeField] private Vector3 _offset;
    [Tooltip("Approximate time for the camera to turn toward the target.")]
    [SerializeField, Min(0.01f)] private float _turnTime = 0.25f;

    [Header("Release")]
    [Tooltip("0 = keep looking until the interaction ends. Set a duration for one-shot interactions that do not send OnEndInteract.")]
    [SerializeField, Min(0f)] private float _releaseAfterSeconds;

    private Iinteractor _interactor;
    private PlayerCameraController _cameraController;
    private bool _previousLookLocked;
    private bool _savedLookLock;
    private bool _forcedLookStarted;
    private Coroutine _releaseRoutine;

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
        if (_interactor == null)
        {
            Debug.LogError($"{nameof(InteractLookTargetAddition)} requires an interactor on {name}.", this);
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

        StopLooking();
    }

    private void HandleInteract(GameObject rootPlayer)
    {
        if (_forcedLookStarted) return;
        if (_lookTarget == null)
        {
            Debug.LogWarning($"{nameof(InteractLookTargetAddition)} has no look target assigned on {name}.", this);
            return;
        }

        if (rootPlayer == null)
        {
            Debug.LogWarning($"{nameof(InteractLookTargetAddition)} received an interaction without a player.", this);
            return;
        }

        _cameraController = rootPlayer.GetComponentInChildren<PlayerCameraController>();
        if (_cameraController == null && Camera.main != null)
            _cameraController = Camera.main.GetComponent<PlayerCameraController>();

        if (_cameraController == null)
        {
            Debug.LogWarning($"{nameof(InteractLookTargetAddition)}: no {nameof(PlayerCameraController)} found for {rootPlayer.name}.", this);
            return;
        }

        _previousLookLocked = _cameraController.IsLookLocked;
        _savedLookLock = true;
        _cameraController.IsLookLocked = false;
        _forcedLookStarted = _cameraController.StartForcedLook(_lookTarget, _offset, float.MaxValue, _turnTime);

        if (!_forcedLookStarted)
        {
            Debug.LogWarning($"{nameof(InteractLookTargetAddition)} could not force the camera to look at {_lookTarget.name}.", this);
            _cameraController.IsLookLocked = _previousLookLocked;
            _cameraController = null;
            _savedLookLock = false;
            return;
        }

        if (_releaseAfterSeconds > 0f)
            _releaseRoutine = StartCoroutine(ReleaseAfterDelay());
    }

    private void HandleEndInteract(GameObject _)
    {
        StopLooking();
    }

    private IEnumerator ReleaseAfterDelay()
    {
        yield return new WaitForSecondsRealtime(_releaseAfterSeconds);
        _releaseRoutine = null;
        StopLooking();
    }

    private void StopLooking()
    {
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }

        if (_cameraController != null)
        {
            if (_forcedLookStarted)
                _cameraController.StopForcedLook();
            if (_savedLookLock)
                _cameraController.IsLookLocked = _previousLookLocked;
        }

        _cameraController = null;
        _savedLookLock = false;
        _forcedLookStarted = false;
    }
}
