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
    [SerializeField] GameObject PositionReferences;
    
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

    public void InstantiateNewTemplat(int ID)
    {
        if (ID > _drawingType.Count - 1 || ID < 0) return;

        splineToLineRenderer = null;

        foreach (DrawingType drawType in _drawingType)
        {
            if (ID != drawType.ID) continue;

            GameObject NewTemplat = Instantiate(drawType.Prefab, PositionReferences.transform.position, PositionReferences.transform.rotation, transform);
            splineToLineRenderer = NewTemplat.GetComponent<SplineToLineRenderer>();
            splineToLineRenderer.AddProgress();
        }
    }

    private void HandleStroke(InputAction.CallbackContext context)
    {
        if (splineToLineRenderer == null) return;

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