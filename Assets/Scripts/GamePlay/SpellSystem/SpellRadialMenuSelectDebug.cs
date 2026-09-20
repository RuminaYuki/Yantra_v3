using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpellRadialMenuSelectDebug : MonoBehaviour
{
    public SpellRadialMenuSelect menuSelect;
    InputSystem_Actions playerInput;

    private void Awake()
    {
        playerInput = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerInput.Enable();
        if (playerInput != null)
        {
            playerInput.Player.Spell.started += HandleOpenRadial;
            playerInput.Player.Spell.canceled += HandleCloseRadial;
        }
    }

    private void OnDisable()
    {
        playerInput.Disable();
        if (playerInput != null)
        {
            playerInput.Player.Spell.started -= HandleOpenRadial;
            playerInput.Player.Spell.canceled -= HandleCloseRadial;
        }
    }

    private void HandleOpenRadial(InputAction.CallbackContext context)
    {
        if (menuSelect != null)
        {
            menuSelect.HandleOpenRadial();
        }
    }
    private void HandleCloseRadial(InputAction.CallbackContext context)
    {
        if (menuSelect != null)
        {
            menuSelect.HandleCloseRadial();
        }
    }
}
