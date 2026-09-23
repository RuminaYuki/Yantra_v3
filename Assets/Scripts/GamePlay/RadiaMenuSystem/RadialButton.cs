using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RadialButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] int _buttonID;
    [SerializeField] GameObject _pivot;
    [SerializeField] GameObject _sprintIcon;
    [SerializeField] Image thisImage;
    [SerializeField] float _angleOffset = 15f;
    [SerializeField] float _selectedScale = 1.1f; // ขนาดตอนถูกเลือก (ปรับใน Inspector)
    float _angleThreshold = 15f;
    [SerializeField] IntEventChannelSO _radialIntID_IEC;
    [SerializeField] VoidEventChannelSO _onSelected;

    private Vector3 _normalScale;
    private RadialManuController _radialManu;

    private void Awake()
    {
        _normalScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (_radialIntID_IEC != null)
        {
            _radialIntID_IEC.Raised += HandleThisIsSelect;
        }
    }

    private void OnDisable()
    {
        if (_radialIntID_IEC != null)
        {
            _radialIntID_IEC.Raised -= HandleThisIsSelect;
        }
    }

    public void SetClass(float fillAmount, int Id, RadialManuController radialManu, RadialMenuButtonData dataSO)
    {
        _buttonID = Id;
        _radialManu = radialManu;

        fillAmount = (fillAmount / 2f);
        float angle = fillAmount * 360f;
        _angleThreshold = angle + _angleOffset;
        _pivot.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);

        RawImage _sprintImage = _sprintIcon.GetComponent<RawImage>();
        _sprintImage.texture = dataSO.buttonIcon;
    }

    private void HandleThisIsSelect(int Id)
    {
        transform.localScale = _buttonID == Id
            ? _normalScale * _selectedScale
            : _normalScale;
    }

    private void Update()
    {
        if (_radialManu != null)
        {
            if (!_radialManu.GetActive()) return;
        }
        else
        {
            Debug.LogWarning($"Wait what {this.gameObject.name} มายังไง??");
        }

        if (Mouse.current != null)
        {
            HandleStroke(Mouse.current.delta.ReadValue());
        }
    }

    private void HandleStroke(Vector2 mouseMovement)
    {
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
                _onSelected.Raise();
            }
        }
    }
}