using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Yantra.UI;

// Load Save screen in the main menu. Shows one SaveSlotUI per save slot.
// A slot with a save loads it; an empty slot starts a New Game in that slot.
// Place the SaveSlotUI objects anywhere under this screen, one per SaveManager.SlotCount.
public class LoadSaveScreen : UIScreen
{
    [Header("Slots")]
    [Tooltip("Filled automatically from the SaveSlotUI objects under this screen.")]
    [FormerlySerializedAs("SaveSlotUIs")]
    [SerializeField] private List<SaveSlotUI> slotUIs = new List<SaveSlotUI>();

    [Header("Database")]
    [SerializeField] private LocationDatabase locationDatabase;

    [Header("Text")]
    [SerializeField] private string emptySlotText = "New Game";
    [SerializeField] private string noDateText = "-";
    [SerializeField] private string dateFormat = "dd/MM/yyyy HH:mm";

    // Called by UIScreen every time this screen opens, so the slots always show the newest saves.
    protected override void OnOpening() => BuildSlots();

    private void BuildSlots()
    {
        SaveManager manager = SaveManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[LoadSaveScreen] No SaveManager in the game.", this);
            return;
        }
        if (locationDatabase == null)
        {
            Debug.LogError("[LoadSaveScreen] Location Database is not assigned.", this);
            return;
        }

        for (int i = 0; i < slotUIs.Count; i++)
        {
            SaveSlotUI slot = slotUIs[i];
            if (slot == null) continue;

            // More slot objects than save slots: hide the extra ones instead of breaking.
            bool inRange = SaveManager.IsValidSlot(i);
            slot.gameObject.SetActive(inRange);
            if (!inRange) continue;

            int slotIndex = i;
            SaveSlotMeta meta = manager.PeekMeta(slotIndex);
            bool hasSave = meta != null && meta.slotIndex == slotIndex;

            if (hasSave)
            {
                slot.SetSlotData(
                    slotIndex,
                    meta.HasSaveTime ? meta.SavedAtLocal.ToString(dateFormat) : noDateText,
                    FormatPlayTime(meta.PlayTime),
                    locationDatabase.GetName(meta.locationId),
                    locationDatabase.GetImage(meta.locationId));
            }
            else
            {
                slot.SetSlotData(slotIndex, "", emptySlotText, "", locationDatabase.FallbackImage);
            }

            Button button = slot.Button;
            if (button == null)
            {
                Debug.LogWarning($"[LoadSaveScreen] Slot {slotIndex} has no Button.", slot);
                continue;
            }

            button.onClick.RemoveAllListeners();
            if (hasSave) button.onClick.AddListener(() => SaveManager.Instance.LoadSave(slotIndex));
            else button.onClick.AddListener(() => SaveManager.Instance.NewGame(slotIndex));
        }
    }

    // hh:mm:ss, hours keep counting past 24 (TimeSpan "hh" would wrap back to 00).
    private static string FormatPlayTime(TimeSpan t)
    {
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var found = new List<SaveSlotUI>(GetComponentsInChildren<SaveSlotUI>(true));
        if (!SameList(found, slotUIs)) slotUIs = found;

        if (slotUIs.Count != SaveManager.SlotCount)
        {
            Debug.LogWarning($"[LoadSaveScreen] {name} has {slotUIs.Count} SaveSlotUI objects, " +
                             $"but there are {SaveManager.SlotCount} save slots.", this);
        }
    }

    private static bool SameList(List<SaveSlotUI> a, List<SaveSlotUI> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
#endif
}
