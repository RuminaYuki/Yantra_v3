using System;
using UnityEngine;

[RequireComponent(typeof(SaveId),typeof(ShootableDoor))]
public class EchantedDoorSaveable : SaveableBehaviour<EchantedDoorSaveable.SaveData>
{
    private ShootableDoor shootableDoor;
    private SaveId saveId;
    protected override string DefaultKey
    {
        get
        {
            if (saveId == null) saveId = GetComponent<SaveId>();
            return saveId != null && saveId.HasId ? $"enchantedDoor_{saveId.Id}" : null;
        }
    }

    private bool _isOpen;

    [Serializable]
    public class SaveData
    {
        public bool isOpen;
    }

    private void Awake()
    {
        shootableDoor = GetComponent<ShootableDoor>();
        saveId = GetComponent<SaveId>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (shootableDoor == null) shootableDoor = GetComponent<ShootableDoor>();
        if (shootableDoor != null) shootableDoor.OnShotAction += HandleOpen;
    }

    protected override void OnDisable()
    {
        if (shootableDoor != null) shootableDoor.OnShotAction -= HandleOpen;
        base.OnDisable();
    }

    protected override SaveData Capture() => new SaveData { isOpen = _isOpen };

    protected override void Restore(SaveData state, int version)
    {
        _isOpen = state.isOpen;
        if (_isOpen)
        {
            shootableDoor.OnShot();
        }
    }

    private void HandleOpen()
    {
        _isOpen = true;
        RecordNow();
    }
}
