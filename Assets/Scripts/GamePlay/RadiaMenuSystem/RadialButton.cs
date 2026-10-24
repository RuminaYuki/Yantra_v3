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
    [SerializeField] Image _lockedImage; // แสดงทับตอนมี item ไม่ครบ (fill เหมือน thisImage)
    [SerializeField] float _angleOffset = 15f;
    [SerializeField] float _selectedScale = 1.1f; // ขนาดตอนถูกเลือก (ปรับใน Inspector)
    float _angleThreshold = 15f;
    [SerializeField] IntEventChannelSO _radialIntID_IEC;
    [SerializeField] VoidEventChannelSO _onSelected;

    private Vector3 _normalScale;
    private RadialManuController _radialManu;
    private bool _available = true;

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

    public void SetClass(float fillAmount, int Id, RadialManuController radialManu, RadialMenuButtonData dataSO, bool available)
    {
        _buttonID = Id;
        _radialManu = radialManu;
        _available = available;

        thisImage.fillAmount = fillAmount;

        if (_lockedImage != null)
        {
            _lockedImage.fillAmount = fillAmount;
            _lockedImage.gameObject.SetActive(!available);
        }

        fillAmount = (fillAmount / 2f);
        float angle = fillAmount * 360f;
        _angleThreshold = angle + _angleOffset;

        // ไอคอนใน prefab วางไว้ทางซ้ายของ pivot (180°) -> หมุนไปที่จุดเริ่ม fill แล้วไปกลางชิ้น
        float originAngle = GetFillOriginAngle(thisImage);
        float halfSlice = thisImage.fillClockwise ? -angle : angle;
        _pivot.transform.localRotation = Quaternion.Euler(0f, 0f, originAngle - 180f + halfSlice);

        RawImage _sprintImage = _sprintIcon.GetComponent<RawImage>();
        _sprintImage.texture = dataSO.buttonIcon;
    }

    // มุมของจุดเริ่ม fill แบบ Radial360 (0° = ขวา, ทวนเข็ม)
    private static float GetFillOriginAngle(Image image)
    {
        switch ((Image.Origin360)image.fillOrigin)
        {
            case Image.Origin360.Bottom: return 270f;
            case Image.Origin360.Right: return 0f;
            case Image.Origin360.Top: return 90f;
            default: return 180f; // Left
        }
    }

    private void HandleThisIsSelect(int Id)
    {
        transform.localScale = _buttonID == Id
            ? _normalScale * _selectedScale
            : _normalScale;
    }

    private void Update()
    {
        // ปุ่มใน pool ที่ยังไม่เคยถูก SetClass จะไม่มี _radialManu
        if (_radialManu == null || !_radialManu.GetActive()) return;

        if (_radialManu.IsSystemEnabled() && Mouse.current != null)
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

            if (angleDifference < _angleThreshold &&
                _radialManu.GetActive() &&
                _radialIntID_IEC != null)
            {
                if (_available)
                {
                    _radialIntID_IEC.Raise(_buttonID);
                    if (_onSelected != null)
                        _onSelected.Raise();
                }
                else
                {
                    // item ไม่ครบ: ล้างการเลือก (เลือกไม่ได้) แต่ยังโชว์ว่าต้องใช้อะไร
                    _radialIntID_IEC.Raise(-1);
                    _radialManu.ShowLockedHover(_buttonID);
                }
            }
        }
    }
}