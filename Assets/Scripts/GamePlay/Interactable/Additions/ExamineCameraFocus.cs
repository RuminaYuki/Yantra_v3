using System;
using System.Collections;
using UnityEngine;

public class ExamineCameraFocus : MonoBehaviour
{
    [SerializeField] private Transform cameraPoint;

    [Header("Transition")]
    [SerializeField] private bool smooth = true;
    [SerializeField] private float transitionDuration = 0.6f;

    private Iinteractor _interactor;
    private PlayerCameraController _cameraController;
    private Transform _cameraTransform;
    private ILocomotionLock _locomotionLock;
    private Vector3 _savedPosition;
    private Quaternion _savedRotation;
    private Coroutine _routine;
    private bool _active;

    private void Awake() => _interactor = GetComponent<Iinteractor>();

    private void OnEnable()
    {
        _interactor.OnInteract += HandleInteract;
        _interactor.OnEndInteract += HandleEndInteract;
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        _interactor.OnEndInteract -= HandleEndInteract;

        if (_active)
        {
            StopRoutine();
            Finish();
        }
    }

    private void HandleInteract(GameObject rootplayer)
    {
        if (_active) return;

        if (cameraPoint == null)
        {
            Debug.LogWarning($"{nameof(ExamineCameraFocus)}: cameraPoint is not assigned on {name}.");
            return;
        }

        if (!ResolveCameraController(rootplayer)) return;

        _savedPosition = _cameraTransform.position;
        _savedRotation = _cameraTransform.rotation;

        _locomotionLock = rootplayer.GetComponentInChildren<ILocomotionLock>();
        _locomotionLock?.LockLocomotion(this);

        _cameraController.IsCutsceneMode = true;
        _active = true;

        MoveTo(cameraPoint.position, cameraPoint.rotation, null);
    }

    private void HandleEndInteract(GameObject rootplayer)
    {
        if (!_active) return;
        MoveTo(_savedPosition, _savedRotation, Finish);
    }

    private bool ResolveCameraController(GameObject rootplayer)
    {
        _cameraController = rootplayer.GetComponentInChildren<PlayerCameraController>();

        if (_cameraController == null && Camera.main != null)
            _cameraController = Camera.main.GetComponent<PlayerCameraController>();

        if (_cameraController == null)
        {
            Debug.LogWarning($"{nameof(ExamineCameraFocus)}: PlayerCameraController not found.");
            return false;
        }

        _cameraTransform = _cameraController.transform;
        return true;
    }

    private void MoveTo(Vector3 pos, Quaternion rot, Action onDone)
    {
        StopRoutine();

        if (!smooth)
        {
            _cameraTransform.SetPositionAndRotation(pos, rot);
            onDone?.Invoke();
            return;
        }

        _routine = StartCoroutine(TransitionRoutine(pos, rot, onDone));
    }

    private IEnumerator TransitionRoutine(Vector3 endPos, Quaternion endRot, Action onDone)
    {
        Vector3 startPos = _cameraTransform.position;
        Quaternion startRot = _cameraTransform.rotation;
        float duration = Mathf.Max(0.01f, transitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _cameraTransform.SetPositionAndRotation(
                Vector3.Lerp(startPos, endPos, t),
                Quaternion.Slerp(startRot, endRot, t));
            yield return null;
        }

        _cameraTransform.SetPositionAndRotation(endPos, endRot);
        _routine = null;
        onDone?.Invoke();
    }

    private void Finish()
    {
        if (_cameraTransform != null)
            _cameraTransform.SetPositionAndRotation(_savedPosition, _savedRotation);

        if (_cameraController != null)
            _cameraController.IsCutsceneMode = false;

        _locomotionLock?.UnlockLocomotion(this);
        _locomotionLock = null;
        _active = false;
    }

    private void StopRoutine()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }
}