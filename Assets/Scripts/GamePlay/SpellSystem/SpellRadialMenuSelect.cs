using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpellRadialMenuSelect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SpellController _spellController;
    [SerializeField] PlayerCameraController _playerCameraController;
    [SerializeField] RadialMenuDataSO _dataSO;
    [SerializeField] GameObject PositionReferences;
    [SerializeField] GameObject OwnerForEffect;

    [Header("Event Channal")]
    [SerializeField] BoolRadialSOAdvEventChannal _boolRadialSOAdvEventChannal;
    [SerializeField] IntEventChannelSO _radialIntID_IEC;

    [Header("Setting")]
    [SerializeField] List<SpellType> _spellTypes = new();

    int _Id;
    bool _radialOpen;

    private void Awake()
    {
        if (_playerCameraController == null)
        {
            _playerCameraController = GetComponentInParent<PlayerCameraController>();
        }
    }

    private void OnEnable()
    {
        if (_spellController != null)
        {
            _spellController.SpellFinished += HandleSpellFinished;
        }

        if (_radialIntID_IEC != null)
        {
            _radialIntID_IEC.Raised += HandleRadialIntID;
        }
    }

    private void OnDisable()
    {
        if (_spellController != null)
        {
            _spellController.SpellFinished -= HandleSpellFinished;
        }

        SetCameraLookLocked(false);

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
        if (!_radialOpen)
            return;

        _Id = ID;
    }

    public void HandleOpenRadial()
    {
        if (_spellController.GetHaveTemplat) return;

        _Id = -1;
        _radialOpen = true;
        SetCameraLookLocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        _boolRadialSOAdvEventChannal.Raise(true, _dataSO);
    }    
    public void HandleCloseRadial()
    {
        if (!_radialOpen)
            return;

        _radialOpen = false;
        InstantiateNewTemplat(_Id);
        CloseRadialMenu();
    }

    public void HandleCancelRadial()
    {
        if (!_radialOpen)
            return;

        _radialOpen = false;
        _Id = -1;
        CloseRadialMenu();
    }

    public void HandleStateExit()
    {
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            HandleCancelRadial();
            return;
        }

        HandleCloseRadial();
    }

    private void CloseRadialMenu()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _boolRadialSOAdvEventChannal.Raise(false, _dataSO);

        if (!_spellController.GetHaveTemplat)
        {
            SetCameraLookLocked(false);
        }
    }

    public void InstantiateNewTemplat(int ID)
    {
        if (ID > _spellTypes.Count - 1 || ID < 0 || _spellController.GetHaveTemplat) return;

        _spellController.SetSplineToLineRenderer(null);

        foreach (SpellType spellType in _spellTypes)
        {
            if (ID != spellType.ID) continue;

            GameObject newTemplate = Instantiate(spellType.Prefab, PositionReferences.transform.position, PositionReferences.transform.rotation, PositionReferences.transform);
            SpellEffectSpawner effectSpawner = newTemplate.GetComponent<SpellEffectSpawner>();
            if (effectSpawner != null)
            {
                effectSpawner.SetSpawnPoint(OwnerForEffect.transform != null ? OwnerForEffect.transform : transform);
            }

            _spellController.SetSplineToLineRenderer(newTemplate.GetComponent<SplineToLineRenderer>());
            _spellController.SetActive(true);
            _spellController.AddProgress();
        }
    }

    private void HandleSpellFinished()
    {
        SetCameraLookLocked(false);
    }

    private void SetCameraLookLocked(bool locked)
    {
        if (_playerCameraController != null)
        {
            _playerCameraController.IsLookLocked = locked;
        }
    }
}

[Serializable]
public class SpellType
{
    public int ID;
    public GameObject Prefab;
}