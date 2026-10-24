using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpellRadialMenuSelect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SpellController _spellController;
    [SerializeField] PlayerCameraController _playerCameraController;
    [Tooltip("แคตตาล็อกการร่ายทั้งหมด (ไม่ถูกแก้ตอน runtime)")]
    [SerializeField] RadialMenuDataSO _dataSO;
    [SerializeField] PlayerInventory _playerInventory; // ว่างไว้ได้ จะหาจากตัว player ให้เอง
    [SerializeField] SpellUnlocks _spellUnlocks; // ว่างไว้ได้ จะหาจากตัว player ให้เอง
    [SerializeField] GameObject PositionReferences;
    [SerializeField] GameObject OwnerForEffect;

    [Header("Event Channal")]
    [SerializeField] BoolRadialSOAdvEventChannal _boolRadialSOAdvEventChannal;
    [SerializeField] IntEventChannelSO _radialIntID_IEC;

    // สำเนาชั่วคราว ใส่เฉพาะการร่ายที่ปลดล็อคแล้ว ส่งให้ RadialManuController
    // index ของปุ่ม = index ใน list นี้
    RadialMenuDataSO _runtimeData;

    int _Id;
    bool _radialOpen;

    public bool HasSelection => _radialOpen && _Id >= 0;

    private void Awake()
    {
        if (_playerCameraController == null)
        {
            _playerCameraController = GetComponentInParent<PlayerCameraController>();
        }

        if (_playerInventory == null)
        {
            _playerInventory = transform.root.GetComponentInChildren<PlayerInventory>();
        }

        if (_spellUnlocks == null)
        {
            _spellUnlocks = transform.root.GetComponentInChildren<SpellUnlocks>();
        }

        if (_spellUnlocks == null)
        {
            Debug.LogWarning("[SpellRadialMenuSelect] No SpellUnlocks on the player. Only 'unlockedByDefault' spells will show.", this);
        }

        _runtimeData = ScriptableObject.CreateInstance<RadialMenuDataSO>();
        _runtimeData.name = _dataSO != null ? $"{_dataSO.name} (Runtime)" : "RadialMenu (Runtime)";
        _runtimeData.hideFlags = HideFlags.DontSave;
        if (_dataSO != null) _runtimeData.IDEventChannel = _dataSO.IDEventChannel;
    }

    private void OnDestroy()
    {
        if (_runtimeData != null) Destroy(_runtimeData);
    }

    // ใส่เฉพาะการร่ายที่ปลดล็อคแล้วลงใน _runtimeData
    private void RebuildRuntimeData()
    {
        _runtimeData.ClearListData();
        if (_dataSO == null) return;

        foreach (RadialMenuButtonData data in _dataSO.GetListData())
        {
            if (data == null) continue;

            bool unlocked = _spellUnlocks != null ? _spellUnlocks.IsUnlocked(data) : data.unlockedByDefault;
            if (unlocked) _runtimeData.AddButtonData(data);
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

    private void HandleRadialIntID(int ID)
    {
        if (!_radialOpen)
            return;

        _Id = ID;
    }

    public void HandleOpenRadial()
    {
        if (_spellController.GetHaveTemplat) return;

        RebuildRuntimeData();
        if (_runtimeData.GetListData().Count == 0) return; // ยังไม่ได้ปลดล็อคการร่ายเลย

        _Id = -1;
        _radialOpen = true;
        SetCameraLookLocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        _boolRadialSOAdvEventChannal.Raise(true, _runtimeData);
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
        _boolRadialSOAdvEventChannal.Raise(false, _runtimeData);

        if (!_spellController.GetHaveTemplat)
        {
            SetCameraLookLocked(false);
        }
    }

    public void InstantiateNewTemplat(int ID)
    {
        if (_spellController.GetHaveTemplat) return;

        var list = _runtimeData.GetListData();
        if (ID < 0 || ID >= list.Count) return;

        RadialMenuButtonData data = list[ID];
        if (data.spellPrefab == null)
        {
            Debug.LogWarning($"[SpellRadialMenuSelect] '{data.buttonName}' has no spellPrefab.", _dataSO);
            return;
        }

        if (!TryConsumeRequiredItem(data)) return;

        _spellController.SetSplineToLineRenderer(null);

        GameObject newTemplate = Instantiate(data.spellPrefab, PositionReferences.transform.position, PositionReferences.transform.rotation, PositionReferences.transform);
        SpellEffectSpawner effectSpawner = newTemplate.GetComponent<SpellEffectSpawner>();
        if (effectSpawner != null)
        {
            effectSpawner.SetSpawnPoint(OwnerForEffect.transform != null ? OwnerForEffect.transform : transform);
            effectSpawner.SetOwner(OwnerForEffect != null ? OwnerForEffect : transform.root.gameObject);
        }

        _spellController.SetSplineToLineRenderer(newTemplate.GetComponent<SplineToLineRenderer>());
        _spellController.SetActive(true);
        _spellController.AddProgress();
    }

    // ใช้ item ตามที่ปุ่มกำหนด ถ้าไม่ครบจะไม่หักอะไรเลยและคืน false
    private bool TryConsumeRequiredItem(RadialMenuButtonData data)
    {
        if (data.itemData == null || data.itemReq <= 0) return true;
        if (_playerInventory == null) return false;

        Inventory inventory = _playerInventory.Inventory;
        if (inventory.TryRemove(data.itemData, data.itemReq, simulate: true) < data.itemReq) return false;

        inventory.TryRemove(data.itemData, data.itemReq);
        return true;
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