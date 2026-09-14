using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DrawRadialManuSelect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] DrawController _drawController;
    [SerializeField] RadialMenuDataSO _dataSO;
    [SerializeField] GameObject PositionReferences;

    [Header("Event Channal")]
    [SerializeField] BoolRadialSOAdvEventChannal _boolRadialSOAdvEventChannal;
    [SerializeField] IntEventChannelSO _radialIntID_IEC;

    [Header("Setting")]
    [SerializeField] List<DrawingType> _drawingType = new();

    InputSystem_Actions _playerInput;
    int _Id;
    bool _isDrawing = false;
    [SerializeField] bool _isActive = false;
    private void Awake()
    {
        _playerInput = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _playerInput.Enable();
        if (_playerInput != null)
        {
            _playerInput.Player.Spell.started += HandleOpenRadial;
            _playerInput.Player.Spell.canceled += HandleCloseRadial;
        } 
        if (_radialIntID_IEC != null)
        {
            _radialIntID_IEC.Raised += HandleRadialIntID;
        }
    }

    private void OnDisable()
    {
        _playerInput.Disable();
            if (_playerInput != null)
        {
            _playerInput.Player.Spell.started -= HandleOpenRadial;
            _playerInput.Player.Spell.canceled -= HandleCloseRadial;
        } 
        if (_radialIntID_IEC != null)
        {
            _radialIntID_IEC.Raised -= HandleRadialIntID;
        }   
    }

    private void OnValidate()
    {
        for (int i = 0; i < _drawingType.Count; i++)
        {
            _drawingType[i].ID = i;
        }
    }

    private void HandleRadialIntID(int ID)
    {
        //Debug.Log($"Radial Menu Button ID: {ID}");
        _Id = ID;
    }

    private void HandleOpenRadial(InputAction.CallbackContext context)
    {
        if (!_isActive) return;

        _Id = -1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        _boolRadialSOAdvEventChannal.Raise(true, _dataSO);
    }    
    private void HandleCloseRadial(InputAction.CallbackContext context)
    {
        InstantiateNewTemplat(_Id);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _boolRadialSOAdvEventChannal.Raise(false, _dataSO);
    }

    public void InstantiateNewTemplat(int ID)
    {
        if (ID > _drawingType.Count - 1 || ID < 0) return;

        _drawController.SetSplineToLineRenderer(null);

        foreach (DrawingType drawType in _drawingType)
        {
            if (ID != drawType.ID) continue;

            GameObject NewTemplat = Instantiate(drawType.Prefab, PositionReferences.transform.position, PositionReferences.transform.rotation, transform);
            _drawController.SetSplineToLineRenderer(NewTemplat.GetComponent<SplineToLineRenderer>());
            _drawController.AddProgress();
        }
    }

    #region API
    /// <summary>
    /// เริ่มการใช้งาน Radial ของการวาดยันต์
    /// </summary>
    /// <param name="value">จำนวนเส้นรัศมี (แฉก) ที่ต้องการให้สะท้อนในการวาดยันต์ เช่น 4, 8, หรือ 16 เส้น</param>
    public void OpenRadial(bool value = true) => _isActive = value;
    #endregion
}

[Serializable]
public class DrawingType
{
    public int ID;
    public GameObject Prefab;
}