using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Yantra.UI;

public class LoadSaveScreen : UIScreen
{
    [Header("Layout")]
    [SerializeField] GameObject slotPrefab;
    [SerializeField] List<SaveSlotUI> SaveSlotUIs = new();

    [Header("Database")]
    [SerializeField] LocationDatabase locationDatabase;

    private void OnValidate()
    {
        if (slotPrefab == null)
        {
            Debug.LogWarning("Slot Prefab is not assigned.");
            return;
        }
        SaveSlotUIs.Clear();
        foreach (Transform child in transform)
        {
            var slotUI = child.GetComponent<SaveSlotUI>();
            if (slotUI != null)
            {
                SaveSlotUIs.Add(slotUI);
            }
        }
    }

    private void Start()
    {
        foreach (SaveSlotUI slot in  SaveSlotUIs)
        {
            int i = SaveSlotUIs.IndexOf(slot);
            SaveSlotMeta meta = SaveManager.Instance.PeekMeta(i);
            //if meta is null or the slotIndex does not match, skip to the next iteration
            if (meta == null || meta.slotIndex != i) continue;

            slot.SetSlotData(
                meta.slotIndex,
                meta.SavedAtLocal.ToString("dd/MM/yyyy HH:mm"),
                meta.playTimeSeconds.ToString(),
                locationDatabase.GetName(meta.locationId),
                locationDatabase.GetImage(meta.locationId)
                );
        }
    }
}
