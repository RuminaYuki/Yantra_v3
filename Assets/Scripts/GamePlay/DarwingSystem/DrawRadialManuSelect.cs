using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public class DrawRadialManuSelect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] DrawController _drawController;
    [SerializeField] RadialMenuDataSO _dataSO;

    [Header("Event Channal")]
    [SerializeField] BoolRadialSOAdvEventChannal _boolRadialSOAdvEventChannal;
    [SerializeField] IntEventChannelSO _radialIntID_IEC;

    InputSystem_Actions _playerInput;
    int _Id;

    private void Awake()
    {
        _playerInput = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _playerInput.Enable();
        if (_playerInput != null)
        {
            _playerInput.Player.YantRadial.started += HandleOpenRadial;
            _playerInput.Player.YantRadial.canceled += HandleCloseRadial;
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
            _playerInput.Player.MousePosition.performed -= HandleOpenRadial;
            _playerInput.Player.YantRadial.canceled -= HandleCloseRadial;
        }

        if (_radialIntID_IEC != null)
        {
            _radialIntID_IEC.Raised -= HandleRadialIntID;
        }   
    }

    private void HandleRadialIntID(int ID)
    {
        Debug.Log($"Radial Menu Button ID: {ID}");
        _Id = ID;
    }

    private void HandleOpenRadial(InputAction.CallbackContext context)
    {
        _Id = -1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        _boolRadialSOAdvEventChannal.Raise(true, _dataSO);
    }    
    private void HandleCloseRadial(InputAction.CallbackContext context)
    {
        _drawController.InstantiateNewTemplat(_Id);
        Cursor.lockState = CursorLockMode.Locked;
        _boolRadialSOAdvEventChannal.Raise(false, _dataSO);
    }
}
