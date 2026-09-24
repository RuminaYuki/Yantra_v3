using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpellController : MonoBehaviour
{
    public event Action SpellFinished;

    [Header("Player Input")]
    private InputSystem_Actions playerInput;

    [Header("References")]
    [SerializeField] SplineToLineRenderer splineToLineRenderer;
    [SerializeField] FadeObject fadeObject;
    [SerializeField] Camera camera;

    [Header("Spell Settings")]
    [SerializeField] float angleThreshold = 10f;

    [Header("Fade Settings")]
    [SerializeField] float fadeDuration = 1.0f;
    [SerializeField] float waitTimeDestroy = 0.5f;

    [Header("Event Channels")]
    [SerializeField] VoidEventChannelSO FinishedSpell;

    private bool _isActive = false;
    private bool _fadeStarted;
    private Coroutine _coroutine;

    private void Awake()
    {
        playerInput = new InputSystem_Actions();
        if (camera == null) camera = Camera.main;
    }

    private void OnEnable()
    {
        playerInput.Enable();
        if (splineToLineRenderer != null)
        {
            LockCursorForSpell();
        }
    }

    private void OnDisable()
    {
        playerInput.Disable();
    }

    private void LateUpdate()
    {
        if (splineToLineRenderer == null) return;

        if (Mouse.current != null && _isActive)
        {
            HandleStroke(Mouse.current.delta.ReadValue());
        }

        if (!_isActive && !_fadeStarted)
        {
            if (_coroutine != null) StopCoroutine(_coroutine);

            Destroy(splineToLineRenderer.gameObject);
        }

        if (!_fadeStarted && splineToLineRenderer.GetProgress() >= 1)
        {
            fadeObject = splineToLineRenderer.gameObject.GetComponent<FadeObject>();
            if (fadeObject == null)
            {
                splineToLineRenderer.gameObject.AddComponent<FadeObject>();
                fadeObject = splineToLineRenderer.gameObject.GetComponent<FadeObject>();
            }

            fadeObject.PlayFade(fadeDuration, 0f, 1f);
            _fadeStarted = true;

            if (_coroutine != null) StopCoroutine(_coroutine);
            _coroutine = StartCoroutine(DestroyObject());

        }
    }

    private void HandleStroke(Vector2 mouseMovement)
    {
        if (splineToLineRenderer == null) return;

        if (mouseMovement.sqrMagnitude > 0.01f)
        {
            Vector2 mouseDirection = mouseMovement.normalized;

            Vector2 splineDirection =
                splineToLineRenderer.GetSplineDirectionScreenSpace(camera);

            float mouseAngle =
                Mathf.Atan2(mouseDirection.y, mouseDirection.x) * Mathf.Rad2Deg;

            float splineAngle =
                Mathf.Atan2(splineDirection.y, splineDirection.x) * Mathf.Rad2Deg;

            float angleDifference =
                Mathf.Abs(Mathf.DeltaAngle(mouseAngle, splineAngle));

            if (angleDifference < angleThreshold)
            {
                splineToLineRenderer.AddProgress();
            }
        }
    }

    IEnumerator DestroyObject()
    {
        float timeDuration = fadeDuration + waitTimeDestroy;

        while (timeDuration > 0)
        {
            timeDuration -= Time.deltaTime;
            //Debug.Log($"timeDuration {timeDuration}");
            yield return null;
        }

        FinishedSpell.Raise();
        SpellFinished?.Invoke();
        Destroy(splineToLineRenderer.gameObject);
    }

    public void SetSplineToLineRenderer(SplineToLineRenderer value)
    {
        splineToLineRenderer = value;
        _fadeStarted = false;
        if (value != null)
        {
            LockCursorForSpell();
        }
    }

    public void SetActive(bool value) => _isActive = value;

    public void AddProgress() => splineToLineRenderer.AddProgress();
    public bool GetHaveTemplat => splineToLineRenderer != null;

    private void LockCursorForSpell()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
