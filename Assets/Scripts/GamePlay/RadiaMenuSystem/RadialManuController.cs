using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RadialManuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField] GameObject _circleUIPrefab;
    [SerializeField] TMP_Text _centerLabel;
    [SerializeField] TMP_Text _requirementLabel; // Item and amount the hovered button needs
    [SerializeField] PlayerInventory _playerInventory; // Optional, found in the scene if empty
    [SerializeField] BoolRadialSOAdvEventChannal _boolRadialSOAdvEventChannal;
    [SerializeField] IntEventChannelSO _radialIntID_IEC; // Same channel as the one on RadialButton

    [Header("Input")]
    [Tooltip("Vector2 delta used to pick a button (e.g. mouse delta or a stick). Empty = Mouse.current.delta.")]
    [SerializeField] InputActionReference _strokeInput;
    [Tooltip("Ignore strokes smaller than this (squared length).")]
    [SerializeField] float _minStrokeSqr = 0.01f;
    [Tooltip("Gamepad only (used when Stroke Input is empty): right stick must be tilted at least this much (squared length) to pick a button.")]
    [SerializeField] float _stickPickSqr = 0.25f;

    [Header("Radial Menu Settings")]
    [SerializeField] float _fillAmountOffeset = 0.001f;

    [Header("Debug")]
    [SerializeField] int _countOfButtons = 6;

    // Pool of buttons, reused across opens (only grows)
    readonly List<GameObject> _radialMenuButtons = new();
    readonly List<RadialButton> _radialButtons = new();

    RadialMenuDataSO _currentData;
    int _hoveredId = -1;
    int _lockedHoverId = -1; // Button without enough items whose labels are shown

    public bool _isActive;
    bool _systemEnabled = true;

    private void Awake()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_centerLabel != null)
            _centerLabel.text = "";

        if (_requirementLabel != null)
            _requirementLabel.text = "";

        if (_playerInventory == null)
            _playerInventory = FindFirstObjectByType<PlayerInventory>();

        SetVisible(false); // Hidden at start
    }

    private void OnEnable()
    {
        if (_boolRadialSOAdvEventChannal != null)
            _boolRadialSOAdvEventChannal.Raised += OpenRadialManu;

        if (_radialIntID_IEC != null)
            _radialIntID_IEC.Raised += HandleHover;
    }

    private void OnDisable()
    {
        if (_boolRadialSOAdvEventChannal != null)
            _boolRadialSOAdvEventChannal.Raised -= OpenRadialManu;

        if (_radialIntID_IEC != null)
            _radialIntID_IEC.Raised -= HandleHover;
    }

    private void Update()
    {
        if (!GetActive()) return;

        Vector2 stroke = ReadStroke();
        if (stroke.sqrMagnitude <= _minStrokeSqr) return;

        RadialButton target = FindButtonInDirection(stroke);
        if (target == null) return;

        SelectButton(target);
    }

    public void OpenRadialManu(bool value, RadialMenuDataSO Data = null)
    {
        if (value && !_systemEnabled)
            return;

        if (value)
        {
            _currentData = Data;
            _countOfButtons = Data != null ? Data.GetListData().Count : _countOfButtons;
            RefreshRadialMenu();
        }

        // Clear hover on both open and close, so no button stays scaled up and no label stays behind
        ClearHover();
        SetVisible(value);
    }

    public void SetSystemEnabled(bool enabled)
    {
        _systemEnabled = enabled;

        if (enabled)
            return;

        ClearHover();
        SetVisible(false);
    }

    public void EnableSystem() => SetSystemEnabled(true);

    public void DisableSystem() => SetSystemEnabled(false);

    private void SetVisible(bool value)
    {
        _isActive = value;
        _canvasGroup.alpha = value ? 1f : 0f;
        _canvasGroup.interactable = value;
        _canvasGroup.blocksRaycasts = value;
    }

    private void ClearHover()
    {
        _hoveredId = -1;
        _lockedHoverId = -1;

        if (_centerLabel != null)
            _centerLabel.text = "";

        if (_requirementLabel != null)
            _requirementLabel.text = "";

        if (_radialIntID_IEC != null)
            _radialIntID_IEC.Raise(-1);
    }

    private void RefreshRadialMenu()
    {
        float fillAmount = (1f / _countOfButtons) - (_countOfButtons > 1 ? _fillAmountOffeset : 0);
        float angle = 360f / _countOfButtons;

        // 1) Grow the pool only when needed
        while (_radialMenuButtons.Count < _countOfButtons)
            CreateButton();

        // 2) Enable only the buttons in use, disable the rest
        var list = _currentData.GetListData();
        for (int i = 0; i < _radialMenuButtons.Count; i++)
        {
            bool inUse = i < _countOfButtons;
            _radialMenuButtons[i].SetActive(inUse);

            if (!inUse) continue;

            _radialMenuButtons[i].transform.localRotation =
                Quaternion.Euler(0f, 0f, -angle * i);

            _radialButtons[i].SetClass(fillAmount, i, list[i], HasRequiredItem(list[i]));
        }
    }

    private void CreateButton()
    {
        GameObject go = Instantiate(
            _circleUIPrefab,
            transform.position,
            Quaternion.identity,
            transform
        );

        _radialMenuButtons.Add(go);
        _radialButtons.Add(go.GetComponent<RadialButton>());
    }

    #region Input

    private Vector2 ReadStroke()
    {
        if (_strokeInput != null && _strokeInput.action != null)
            return _strokeInput.action.ReadValue<Vector2>();

        // Gamepad: push the right stick toward a button. Small tilts are ignored
        // so the stick resting near the center does not flick between buttons.
        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.rightStick.ReadValue();
            if (stick.sqrMagnitude >= _stickPickSqr)
                return stick;
        }

        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    }

    // Picks the button whose direction is closest to the stroke.
    // Choosing the closest one means only one button can win, even if thresholds overlap.
    private RadialButton FindButtonInDirection(Vector2 stroke)
    {
        float strokeAngle = Mathf.Atan2(stroke.y, stroke.x) * Mathf.Rad2Deg;

        RadialButton best = null;
        float bestDiff = float.MaxValue;

        for (int i = 0; i < _countOfButtons && i < _radialButtons.Count; i++)
        {
            RadialButton button = _radialButtons[i];
            Vector2 dir = button.IconDirection;
            float buttonAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float diff = Mathf.Abs(Mathf.DeltaAngle(strokeAngle, buttonAngle));

            if (diff < button.AngleThreshold && diff < bestDiff)
            {
                best = button;
                bestDiff = diff;
            }
        }

        return best;
    }

    // Only raises events when the hovered button changes, not every frame.
    private void SelectButton(RadialButton button)
    {
        int id = button.ButtonID;

        if (button.IsAvailable)
        {
            if (_hoveredId == id) return;

            if (_radialIntID_IEC != null)
                _radialIntID_IEC.Raise(id); // HandleHover updates _hoveredId and the labels
            else
            {
                _hoveredId = id;
                ShowLabels(id);
            }
            button.NotifySelected();
        }
        else
        {
            // Not enough items: clear the selection (cannot pick) but still show what it needs
            if (_lockedHoverId == id) return;

            if (_radialIntID_IEC != null)
                _radialIntID_IEC.Raise(-1);

            _hoveredId = -1;
            _lockedHoverId = id;
            ShowLabels(id);
        }
    }

    #endregion

    private void HandleHover(int id)
    {
        _lockedHoverId = -1;

        // The channel can be raised with the same ID again; skip to avoid resetting the text
        if (id == _hoveredId) return;

        _hoveredId = id;
        ShowLabels(id);
    }

    private void ShowLabels(int id)
    {
        if (_centerLabel != null)
            _centerLabel.text = GetLabel(id);

        if (_requirementLabel != null)
            _requirementLabel.text = GetRequirement(id);
    }

    private bool HasRequiredItem(RadialMenuButtonData data)
    {
        if (data.itemData == null || data.itemReq <= 0) return true;
        return CountOf(data.itemData) >= data.itemReq;
    }

    private int CountOf(ItemData item) =>
        _playerInventory != null && _playerInventory.Inventory != null
            ? _playerInventory.Inventory.CountOf(item)
            : 0;

    private string GetRequirement(int id)
    {
        if (id < 0 || _currentData == null) return "";

        var list = _currentData.GetListData();
        if (id >= list.Count) return "";

        RadialMenuButtonData data = list[id];
        if (data.itemData == null || data.itemReq <= 0) return "";

        string itemName = string.IsNullOrEmpty(data.itemData.displayName)
            ? data.itemData.name
            : data.itemData.displayName;

        return $"{itemName} {CountOf(data.itemData)}/{data.itemReq}";
    }

    private string GetLabel(int id)
    {
        if (id < 0 || _currentData == null) return "";

        var list = _currentData.GetListData();
        return id < list.Count ? list[id].buttonName : "";
    }

    public bool GetActive() => _isActive && _systemEnabled;

    public bool IsSystemEnabled() => _systemEnabled;
}
