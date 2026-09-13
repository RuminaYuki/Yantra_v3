using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class DrawController : MonoBehaviour
{
    [Header("Player Input")]
    private InputSystem_Actions playerInput;

    [Header("References")]
    [SerializeField] SplineToLineRenderer splineToLineRenderer;
    [SerializeField] Camera camera;

    [Header("Settings")]
    [SerializeField] float angleThreshold = 10f;


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
            LockCursorForDrawing();
        }
    }

    private void OnDisable()
    {
        playerInput.Disable();
    }

    private void Update()
    {
        if (splineToLineRenderer == null)
        {
            return;
        }

        if (Mouse.current != null)
        {
            HandleStroke(Mouse.current.delta.ReadValue());
        }

        if (splineToLineRenderer.GetProgress() >= 1)
        {
            Destroy(splineToLineRenderer.gameObject);
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

    public void SetSplineToLineRenderer(SplineToLineRenderer value)
    {
        splineToLineRenderer = value;
        if (value != null)
        {
            LockCursorForDrawing();
        }
    }

    public void AddProgress() => splineToLineRenderer.AddProgress();

    private void LockCursorForDrawing()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
