using UnityEngine;
using UnityEngine.UI;

// View-only slice of the radial menu. RadialManuController reads input and decides
// which button is hovered; this class only draws itself and reacts to the hovered ID.
public class RadialButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] int _buttonID;
    [SerializeField] GameObject _pivot;
    [SerializeField] GameObject _sprintIcon;
    [SerializeField] GameObject _endCapPivot;
    [SerializeField] Image thisImage;
    [SerializeField] Image _lockedImage;
    [SerializeField] float _angleOffset = 15f;
    [SerializeField] float _selectedScale = 1.1f;
    float _angleThreshold = 15f;
    [SerializeField] IntEventChannelSO _radialIntID_IEC;
    [SerializeField] VoidEventChannelSO _onSelected;

    private Vector3 _normalScale;
    private bool _available = true;

    public int ButtonID => _buttonID;
    public bool IsAvailable => _available;

    // Max angle (degrees) between the stroke and this button's direction to count as a hit.
    public float AngleThreshold => _angleThreshold;

    // Direction from the menu center to this button's icon, in screen space.
    public Vector2 IconDirection =>
        (_sprintIcon.transform.position - _pivot.transform.position).normalized;

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

    public void SetClass(float fillAmount, int Id, RadialMenuButtonData dataSO, bool available)
    {
        _buttonID = Id;
        _available = available;

        thisImage.fillAmount = fillAmount;
        _endCapPivot.transform.localRotation = Quaternion.Euler(0f, 0f, fillAmount * -360f);

        if (_lockedImage != null)
        {
            _lockedImage.fillAmount = fillAmount;
            _lockedImage.gameObject.SetActive(!available);
        }

        fillAmount = (fillAmount / 2f);
        float angle = fillAmount * 360f;
        _angleThreshold = angle + _angleOffset;

        // The icon in the prefab sits left of the pivot (180°): rotate it to the fill start, then to the middle of the slice.
        float originAngle = GetFillOriginAngle(thisImage);
        float halfSlice = thisImage.fillClockwise ? -angle : angle;
        _pivot.transform.localRotation = Quaternion.Euler(0f, 0f, originAngle - 180f + halfSlice);

        RawImage _sprintImage = _sprintIcon.GetComponent<RawImage>();
        _sprintImage.texture = dataSO.buttonIcon;
    }

    // Called by RadialManuController once when this button becomes the selected one.
    public void NotifySelected()
    {
        if (_onSelected != null)
            _onSelected.Raise();
    }

    // Angle of the Radial360 fill origin (0° = right, counter-clockwise).
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
}
