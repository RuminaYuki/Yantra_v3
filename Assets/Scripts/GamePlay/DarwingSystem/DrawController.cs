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
    [SerializeField] List<DrawingType> _drawingType = new();

    private Vector2 lastMousePos;

    private void Awake()
    {
        playerInput = new InputSystem_Actions();
        if (camera == null) camera = Camera.main;
    }

    private void OnEnable()
    {
        playerInput.Enable();
        if (playerInput != null)
        {
            playerInput.Player.MousePosition.performed += HandleStroke;
        }
    }

    private void OnDisable()
    {
        playerInput.Disable();
        if (playerInput != null)
        {
            playerInput.Player.MousePosition.performed -= HandleStroke;
        }
    }

    private void HandleStroke(InputAction.CallbackContext context)
    {
        Vector2 mousePosition = context.action.ReadValue<Vector2>();
        Vector2 mouseMovement = mousePosition - lastMousePos;

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

        lastMousePos = mousePosition;
    }

    public List<DrawingType> GetListDrawingType() => _drawingType;
}

[Serializable]
public struct DrawingType
{
    public int ID;
    public GameObject Prefab;
}