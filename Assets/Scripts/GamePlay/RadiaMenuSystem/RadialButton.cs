using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class RadialButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] int _buttonID;
    [SerializeField] GameObject _pivot;
    [SerializeField] GameObject _sprintIcon;
    [SerializeField] Image thisImage;
    [FormerlySerializedAs("_sprintIcon")]
    [SerializeField] float _angleOffset = 15f;
    float _angleThreshold = 15f;
    [SerializeField] IntEventChannelSO _radialIntID_IEC;
    private InputSystem_Actions playerInput;
    private Vector2 lastMousePos;

    private void Awake()
    {
        playerInput = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerInput.Enable();
        if (playerInput != null)
        {
            playerInput.Player.MousePosition.performed += HandleStroke;
        }
        if (_radialIntID_IEC != null)
        {
            _radialIntID_IEC.Raised += HandleThisIsSelect;
        }
    }

    private void OnDisable()
    {
        playerInput.Disable();
        if (playerInput != null)
        {
            playerInput.Player.MousePosition.performed -= HandleStroke;
        }
        if(_radialIntID_IEC != null)
        {
            _radialIntID_IEC.Raised += HandleThisIsSelect;
        }
    }

    public void FixRotation(float fillAmount, int Id)
    {
        _buttonID = Id;

        fillAmount = (fillAmount / 2f);
        float angle = fillAmount * 360f;
        _angleThreshold = angle + _angleOffset;
        _pivot.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private void HandleThisIsSelect(int Id)
    {
        if (_buttonID == Id)
        {
            thisImage.color = Color.green;
        }
        else
        {
            thisImage.color = Color.white;
        }
    }

    private void HandleStroke(InputAction.CallbackContext context)
    {
        Vector2 mousePosition = context.action.ReadValue<Vector2>();
        Vector2 mouseMovement = mousePosition - lastMousePos;

        Vector2 iconDirection = (_sprintIcon.transform.position - _pivot.transform.position).normalized;

        if (mouseMovement.sqrMagnitude > 0.01f)
        {
            Vector2 mouseDirection = mouseMovement.normalized;

            float mouseAngle =
                Mathf.Atan2(mouseDirection.y, mouseDirection.x) * Mathf.Rad2Deg;

            float splineAngle =
                Mathf.Atan2(iconDirection.y, iconDirection.x) * Mathf.Rad2Deg;

            float angleDifference =
                Mathf.Abs(Mathf.DeltaAngle(mouseAngle, splineAngle));

            if (angleDifference < _angleThreshold)
            {
                _radialIntID_IEC.Raise(_buttonID);
            }
        }

        lastMousePos = mousePosition;
    }
}