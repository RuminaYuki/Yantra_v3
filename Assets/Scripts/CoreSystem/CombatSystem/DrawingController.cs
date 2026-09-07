using UnityEngine;
using UnityEngine.InputSystem;

public class DrawingController : MonoBehaviour
{
    [Header("Player Input")]
    private InputSystem_Actions _playerInput;

    [Header("References")]
    [SerializeField] private StrokeDrawer strokeDrawer;
    [SerializeField] private PathPoint pathPoint;

    private void Awake()
    {
        _playerInput = new InputSystem_Actions();
        if (strokeDrawer == null) { strokeDrawer = GetComponent<StrokeDrawer>(); }
    }

    private void OnEnable()
    {
        _playerInput.Enable();
        if (_playerInput != null)
        {
            _playerInput.Player.MousePosition.performed += HandleStroke;
        }
    }

    private void OnDisable()
    {
        _playerInput.Disable();
        if (_playerInput != null)
        {
            _playerInput.Player.MousePosition.performed -= HandleStroke;
        }
    }

    private void HandleStroke(InputAction.CallbackContext context)
    {
        Camera cam = Camera.main;

        Vector2 mousePosition = context.action.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            PathPointData nextPoint = pathPoint?.CheckNextPoint(hit.point);

            if (nextPoint != null && nextPoint.Transform != null)
            {
                strokeDrawer.DrawStroke(nextPoint.Transform.position);
            }
        }

#if UNITY_EDITOR
        DebugRayCast(ray);
#endif
    }

    private void DebugRayCast(Ray ray)
    {
        Debug.DrawRay(ray.origin, ray.direction * 10f, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            Debug.DrawLine(hit.point, hit.point + Vector3.up * 0.5f, Color.green);
        }
    }
}
