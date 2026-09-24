using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RadialManuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField] GameObject _circleUIPrefab;
    [SerializeField] TMP_Text _centerLabel;
    [SerializeField] BoolRadialSOAdvEventChannal _boolRadialSOAdvEventChannal;
    [SerializeField] IntEventChannelSO _radialIntID_IEC; // �� channel ���ǡѺ��� RadialButton ��

    [Header("Radial Menu Settings")]
    [SerializeField] float _fillAmountOffeset = 0.001f;

    [Header("Debug")]
    [SerializeField] int _countOfButtons = 6;

    // pool: ������������ҧ�������� (��駷���Դ��лԴ����)
    readonly List<GameObject> _radialMenuButtons = new();
    readonly List<Image> _buttonImages = new();
    readonly List<RadialButton> _radialButtons = new();

    RadialMenuDataSO _currentData;
    int _hoveredId = -1;

    public bool _isActive;
    bool _systemEnabled = true;

    private void Awake()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_centerLabel != null)
            _centerLabel.text = "";

        SetVisible(false); // ���͹ ����ͧ���ҧ����
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

    public void OpenRadialManu(bool value, RadialMenuDataSO Data = null)
    {
        if (value && !_systemEnabled)
            return;

        if (value)
        {
            _currentData = Data;
            _countOfButtons = Data != null ? Data.GetListData().Count : _countOfButtons;
            RefreshRadialMenu();

            // ��ҧʶҹ� hover ��ҧ�ҡ�ͺ��͹ (������Ѻ scale ���� + label ��ҧ)
            if (_radialIntID_IEC != null)
                _radialIntID_IEC.Raise(-1);
        }

        SetVisible(value);
    }

    public void SetSystemEnabled(bool enabled)
    {
        _systemEnabled = enabled;

        if (enabled)
            return;

        _hoveredId = -1;
        if (_centerLabel != null)
            _centerLabel.text = "";

        if (_radialIntID_IEC != null)
            _radialIntID_IEC.Raise(-1);

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

    private void RefreshRadialMenu()
    {
        float fillAmount = (1f / _countOfButtons) - _fillAmountOffeset;
        float angle = 360f / _countOfButtons;

        // 1) ��������� pool ੾�е͹�������
        while (_radialMenuButtons.Count < _countOfButtons)
            CreateButton();

        // 2) �Դ੾�л�������ͧ�� �������ͻԴ
        for (int i = 0; i < _radialMenuButtons.Count; i++)
        {
            bool inUse = i < _countOfButtons;
            _radialMenuButtons[i].SetActive(inUse);

            if (!inUse) continue;

            _buttonImages[i].fillAmount = fillAmount;
            _radialMenuButtons[i].transform.localRotation =
                Quaternion.Euler(0f, 0f, -angle * i);

            var list = _currentData.GetListData();
            _radialButtons[i].SetClass(fillAmount, i, this, list[i]);
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
        _buttonImages.Add(go.GetComponent<Image>());
        _radialButtons.Add(go.GetComponent<RadialButton>());
    }

    private void HandleHover(int id)
    {
        // channel �١ Raise �ء������������Ѻ �ѹ�� text ���
        if (id == _hoveredId) return;

        _hoveredId = id;

        if (_centerLabel != null)
            _centerLabel.text = GetLabel(id);
    }

    private string GetLabel(int id)
    {
        Debug.Log($"GetLabel id={id}, data={(_currentData == null ? "NULL" : _currentData.name)}");

        if (id < 0 || _currentData == null) return "";

        var list = _currentData.GetListData();
        return id < list.Count ? list[id].buttonName : "";
    }

    public bool GetActive() => _isActive && _systemEnabled;

    public bool IsSystemEnabled() => _systemEnabled;
}