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

    int _Id;

    private void OnEnable()
    {
        if (_radialIntID_IEC != null)
        {
            _radialIntID_IEC.Raised += HandleRadialIntID;
        }
    }

    private void OnDisable()
    {
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
        _Id = ID;
    }

    public void HandleOpenRadial()
    {
        _Id = -1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        _boolRadialSOAdvEventChannal.Raise(true, _dataSO);
    }    
    public void HandleCloseRadial()
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
}

[Serializable]
public class SpellType
{
    public int ID;
    public GameObject Prefab;
}