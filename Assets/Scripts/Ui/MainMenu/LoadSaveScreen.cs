using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yantra.UI;

public class LoadSaveScreen : UIScreen
{
    [Header("Layout")]
    [SerializeField] GameObject slotPrefab;
    [SerializeField] List<SaveSlotUI> SaveSlotUIs = new();

    [Header("Database")]
    [SerializeField] LocationDatabase locationDatabase;

    private void OnEnable()
    {
        BuildSlot();
    }

    private void OnDisable()
    {
    }

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
        BuildSlot();
    }

    private void BuildSlot()
    {
        foreach (SaveSlotUI slot in SaveSlotUIs)
        {
            int i = SaveSlotUIs.IndexOf(slot);
            SaveSlotMeta meta = SaveManager.Instance.PeekMeta(i);
            //if meta is null or the slotIndex does not match, skip to the next iteration

            bool isValidMeta = meta != null && meta.slotIndex == i;

            Button button = slot.gameObject.GetComponent<Button>();

            switch (isValidMeta)
            {
                case true:
                    slot.SetSlotData(
                        meta.slotIndex,
                        meta.SavedAtLocal.ToString("dd/MM/yyyy HH:mm"),
                        TimeSpan.FromSeconds(meta.playTimeSeconds).ToString(@"hh\:mm\:ss"),
                        locationDatabase.GetName(meta.locationId),
                        locationDatabase.GetImage(meta.locationId)
                    );

                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => SaveManager.Instance.LoadSave(meta.slotIndex));
                    }
                    else
                    {
                        Debug.LogWarning($"[LoadSaveScreen] Slot {i} does not have a Button component.");
                    }
                    break;
                case false:
                    slot.SetSlotData(
                        i,
                        "",
                        "New Game",
                        "",
                        locationDatabase.FallbackImage
                    );

                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => SaveManager.Instance.NewGame(i));
                    }
                    else
                    {
                        Debug.LogWarning($"[LoadSaveScreen] Slot {i} does not have a Button component.");
                    }
                    continue;
            }
        }
    }
}
