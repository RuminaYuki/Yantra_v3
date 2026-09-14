using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpellRadialMenuSelect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SpellController _spellController;
    [SerializeField] RadialMenuDataSO _dataSO;
    [SerializeField] GameObject PositionReferences;

    [Header("Event Channal")]
    [SerializeField] BoolRadialSOAdvEventChannal _boolRadialSOAdvEventChannal;
    [SerializeField] IntEventChannelSO _radialIntID_IEC;

    [Header("Setting")]
    [SerializeField] List<SpellType> _spellTypes = new();

    InputSystem_Actions _playerInput;
    int _Id;
    bool _isCasting = false;
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
        for (int i = 0; i < _spellTypes.Count; i++)
        {
            _spellTypes[i].ID = i;
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
        if (ID > _spellTypes.Count - 1 || ID < 0) return;

        _spellController.SetSplineToLineRenderer(null);

        foreach (SpellType spellType in _spellTypes)
        {
            if (ID != spellType.ID) continue;

            GameObject newTemplate = Instantiate(spellType.Prefab, PositionReferences.transform.position, PositionReferences.transform.rotation, transform);
            _spellController.SetSplineToLineRenderer(newTemplate.GetComponent<SplineToLineRenderer>());
            _spellController.AddProgress();
        }
    }

    #region API
    /// <summary>
    /// เริ่มการใช้งาน Radial ของการร่ายคาถา
    /// </summary>
    /// <param name="value">จำนวนเส้นรัศมี (แฉก) ที่ต้องการให้สะท้อนในการวาดยันต์ เช่น 4, 8, หรือ 16 เส้น</param>
    public void OpenRadial(bool value = true) => _isActive = value;
    #endregion
}

[Serializable]
public class SpellType
{
    public int ID;
    public GameObject Prefab;
}