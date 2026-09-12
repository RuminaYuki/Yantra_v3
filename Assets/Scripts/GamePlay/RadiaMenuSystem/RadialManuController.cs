using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class RadialManuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField] GameObject _circleUIPrefab;
    [SerializeField] BoolRadialSOAdvEventChannal _boolRadialSOAdvEventChannal;

    [Header("Radial Menu Settings")]
    [SerializeField] float _fillAmountOffeset = 0.001f;

    [Header("Debug")]
    [SerializeField] int _countOfButtons = 6;

    List<GameObject> _radialMenuButtons = new();

    float _currentCountOfButtons = 0;

    private void Awake()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        OpenRadialManu(false);
    }

    private void OnEnable()
    {
        if (_boolRadialSOAdvEventChannal != null)
        {
            _boolRadialSOAdvEventChannal.Raised += OpenRadialManu;
        }
    }

    private void OnDisable()
    {
        if (_boolRadialSOAdvEventChannal != null)
        {
            _boolRadialSOAdvEventChannal.Raised -= OpenRadialManu;
        }
    }

    public void OpenRadialManu(bool value, RadialMenuDataSO Data = null)
    {
        _countOfButtons = Data != null ? Data.GetListData().Count : _countOfButtons;
        CreateRadialMenu(Data);

        _canvasGroup.alpha = value ? 1f : 0f;
        _canvasGroup.interactable = value;
        _canvasGroup.blocksRaycasts = value;
    }

    private void CreateRadialMenu(RadialMenuDataSO Data)
    {
        float fillAmount = (1f / _countOfButtons) - _fillAmountOffeset;

        ClearList();

        for (int i = 0; i < _countOfButtons; i++)
        {
            GameObject radialMenu = Instantiate(
                _circleUIPrefab,
                transform.position,
                Quaternion.identity,
                transform
            );

            Image buttonImage = radialMenu.GetComponent<Image>();
            buttonImage.fillAmount = fillAmount;

            float angle = 360f / _countOfButtons;
            float buttonAngle = angle * i;

            radialMenu.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                -buttonAngle
            );

            RadialButton radialButton = radialMenu.GetComponent<RadialButton>();
            radialButton.FixRotation(fillAmount, i);

            _radialMenuButtons.Add(radialMenu);
        }
    }



    private void ClearList()
    {
        foreach (var button in _radialMenuButtons)
        {
            Destroy(button);
        }
        _radialMenuButtons.Clear();
    }
}