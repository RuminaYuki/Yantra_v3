using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class SpellController : MonoBehaviour
{
    public event Action SpellFinished;

    [Header("References")]
    [SerializeField] SplineToLineRenderer splineToLineRenderer;
    [FormerlySerializedAs("camera")]
    [SerializeField] Camera _camera;

    [Header("Spell Settings")]
    [Tooltip("มุมต่างสูงสุด (องศา) ระหว่างทิศเมาส์กับทิศเส้น ที่ยังนับว่าวาดถูก")]
    [SerializeField] float angleThreshold = 10f;

    [Header("Gamepad Drawing")]
    [Tooltip("Max angle (degrees) between the right stick and the line that still counts. Sticks are less precise than a mouse, so this is wider.")]
    [SerializeField] float stickAngleThreshold = 30f;
    [Tooltip("Right stick must be tilted at least this much (0-1) to draw.")]
    [SerializeField, Range(0.1f, 1f)] float stickMinTilt = 0.5f;
    [Tooltip("Seconds (real time) between progress steps while the stick is held. Keeps the speed the same at any frame rate.")]
    [SerializeField, Min(0f)] float stickStepInterval = 0.02f;

    [Header("Fade Settings")]
    [SerializeField] float fadeDuration = 1.0f;
    [SerializeField] float waitTimeDestroy = 0.5f;

    [Header("Event Channels")]
    [SerializeField] VoidEventChannelSO FinishedSpell;

    [Header("Time")]
    [Range(0f, 1f)][SerializeField] private float _worldScale = 0.05f; // 0 = full stop

    private bool _isActive;
    private bool _fadeStarted;
    private Coroutine _finishRoutine;
    private float _nextStickStepTime;

    public bool GetHaveTemplat => splineToLineRenderer != null;
    public void AddProgress() => splineToLineRenderer.AddProgress();

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void OnEnable()
    {
        if (GetHaveTemplat) LockCursorForSpell();
    }

    private void LateUpdate()
    {
        if (!GetHaveTemplat) return;

        // ใส่ซ้ำทุกเฟรม กันโดนคืนเป็น 1 จาก pause menu / hitstop
        if (_isActive) ApplySlowMotion();

        if (_isActive && !_fadeStarted) HandleGamepadStroke();

        if (!_fadeStarted && splineToLineRenderer.GetProgress() >= 1f)
        {
            StartFinish();
        }
    }

    public void SetSplineToLineRenderer(SplineToLineRenderer value)
    {
        splineToLineRenderer = value;
        _fadeStarted = false;

        if (value != null) LockCursorForSpell();
    }

    public void SetActive(bool value)
    {
        _isActive = value;

        if (_isActive)
        {
            if (GetHaveTemplat) ApplySlowMotion();
            return;
        }

        // ปิดก่อนวาดเสร็จ = ยกเลิก ลบ template ทิ้ง
        // ถ้าวาดเสร็จแล้ว (กำลัง fade) ปล่อยให้ FinishRoutine จัดการเอง
        if (GetHaveTemplat && !_fadeStarted)
        {
            CancelSpell();
        }
    }

    public void HandleStroke(Vector2 mouseMovement)
    {
        TryStroke(mouseMovement, angleThreshold);
    }

    // Gamepad: hold the right stick in the direction of the line.
    private void HandleGamepadStroke()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return;

        Vector2 stick = pad.rightStick.ReadValue();
        if (stick.sqrMagnitude < stickMinTilt * stickMinTilt) return;
        if (Time.unscaledTime < _nextStickStepTime) return;

        if (TryStroke(stick, stickAngleThreshold))
            _nextStickStepTime = Time.unscaledTime + stickStepInterval;
    }

    private bool TryStroke(Vector2 direction, float maxAngle)
    {
        if (!GetHaveTemplat) return false;
        if (direction.sqrMagnitude <= 0.01f) return false;

        Vector2 splineDirection = splineToLineRenderer.GetSplineDirectionScreenSpace(_camera);
        if (splineDirection.sqrMagnitude < 0.0001f) return false;

        float angleDifference = Vector2.Angle(direction, splineDirection);
        if (angleDifference >= maxAngle) return false;

        splineToLineRenderer.AddProgress();
        return true;
    }

    private void StartFinish()
    {
        _fadeStarted = true;

        GameObject template = splineToLineRenderer.gameObject;
        if (!template.TryGetComponent(out FadeObject fadeObject))
        {
            fadeObject = template.AddComponent<FadeObject>();
        }
        fadeObject.PlayFade(fadeDuration, 0f, 1f);

        if (_finishRoutine != null) StopCoroutine(_finishRoutine);
        _finishRoutine = StartCoroutine(FinishRoutine(template));
    }

    // ใช้เวลาจริง เพราะระหว่างนี้ timeScale ยังเป็น slow motion อยู่
    private IEnumerator FinishRoutine(GameObject template)
    {
        yield return new WaitForSecondsRealtime(fadeDuration + waitTimeDestroy);

        _finishRoutine = null;

        if (FinishedSpell != null) FinishedSpell.Raise();
        SpellFinished?.Invoke();

        // Destroy ไม่ลบทันที ต้องปล่อย reference เอง
        // ไม่งั้น LateUpdate เฟรมนี้ยังเห็น template แล้วใส่ slow motion ทับกลับ
        if (splineToLineRenderer != null && splineToLineRenderer.gameObject == template)
        {
            splineToLineRenderer = null;
        }
        if (template != null) Destroy(template);
        RestoreTime();
    }

    private void CancelSpell()
    {
        if (_finishRoutine != null)
        {
            StopCoroutine(_finishRoutine);
            _finishRoutine = null;
        }

        Destroy(splineToLineRenderer.gameObject);
        splineToLineRenderer = null;
        //RestoreTime();
    }

    //timeScale = 0 แปลว่า pause / hitstop / game over กำลังคุมเวลาอยู่ ห้ามไปทับ
    private void ApplySlowMotion()
    {
        if (Time.timeScale > 0f) Time.timeScale = _worldScale;
    }

    private void RestoreTime()
    {
        if (Time.timeScale > 0f) Time.timeScale = 1f;
    }

    private void LockCursorForSpell()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
