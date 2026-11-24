using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Yantra.UI;

// Load Save screen in the main menu. Shows one SaveSlotUI per save slot.
//
// Click a slot to select it, then use the buttons at the bottom:
//   Load   - loads the selected save, or starts a New Game if the slot is empty.
//   Delete - press twice to delete the selected save (the label asks to confirm).
// Clicking a slot only selects it; nothing loads until Load / New Game is pressed.
//
// Place the SaveSlotUI objects anywhere under this screen, one per SaveManager.SlotCount.
public class LoadSaveScreen : UIScreen
{
    [Header("Slots")]
    [Tooltip("Filled automatically from the SaveSlotUI objects under this screen.")]
    [FormerlySerializedAs("SaveSlotUIs")]
    [SerializeField] private List<SaveSlotUI> slotUIs = new List<SaveSlotUI>();

    [Header("Database")]
    [SerializeField] private LocationDatabase locationDatabase;

    [Header("Bottom Buttons")]
    [Tooltip("Loads the selected slot. Says New Game when the slot is empty.")]
    [SerializeField] private Button loadButton;

    [Tooltip("Deletes the selected save. Must be pressed twice.")]
    [SerializeField] private Button deleteButton;

    [Tooltip("Seconds the Delete button waits for the second press.")]
    [SerializeField, Min(0.5f)] private float deleteConfirmTime = 3f;

    [Header("Selection Marker (optional)")]
    [Tooltip("The dot on the line at the left. Slides to the selected slot.")]
    [SerializeField] private RectTransform marker;

    [SerializeField, Min(0f)] private float markerSpeed = 14f;

    [Header("Background (optional)")]
    [Tooltip("The picture behind the dark shade.")]
    [SerializeField] private Image background;
    [SerializeField] private Sprite defaultBackground;

    [Tooltip("Show the selected slot's location picture as the background.\n" +
             "Off = keep the picture that is set on the Image.")]
    [SerializeField] private bool backgroundFollowsSelection = false;

    [SerializeField, Min(0f)] private float backgroundFade = 0.25f;

    [Header("Text")]
    [Tooltip("Shown as the place name of a slot with no save.")]
    [SerializeField] private string emptyText = "Empty Slot";
    [SerializeField] private string datePrefix = "Date : ";
    [Tooltip("dd = day, MM = month, yy = year, HH:mm:ss = time")]
    [SerializeField] private string saveTimeFormat = "dd/MM/yy   HH:mm:ss";
    [SerializeField] private string noDateText = "-";
    [SerializeField] private string playTimePrefix = "Time Played : ";
    [SerializeField] private string loadLabel = "Load";
    [SerializeField] private string newGameLabel = "New Game";
    [SerializeField] private string deleteLabel = "Delete";
    [SerializeField] private string confirmDeleteLabel = "Sure?";

    private bool[] _hasSave = Array.Empty<bool>();
    private Sprite[] _images = Array.Empty<Sprite>();
    private SaveSlotSelectedMark[] _marks = Array.Empty<SaveSlotSelectedMark>();
    private int _selected = -1;

    // Unscaled time when the Delete confirm runs out. Below 0 = not waiting for a second press.
    private float _confirmUntil = -1f;

    private TMP_Text _loadText;
    private TMP_Text _deleteText;

    private Vector3 _markerHome;
    private bool _markerHomeSet;
    private float _markerY;

    private Sprite _bgDefaultSprite;
    private Color _bgDefaultColor;
    private Color _bgBaseColor;
    private bool _bgCached;
    private float _bgAlpha = 1f;

    private bool HasSelection =>
        _selected >= 0 && _selected < _hasSave.Length &&
        slotUIs[_selected] != null && slotUIs[_selected].gameObject.activeSelf;

    private bool IsConfirmingDelete => _confirmUntil >= 0f;

    protected override void Awake()
    {
        base.Awake();

        _loadText = Hook(loadButton, OnLoadClicked);
        _deleteText = Hook(deleteButton, OnDeleteClicked);

        if (loadButton == null)
            Debug.LogWarning("[LoadSaveScreen] Load Button is not assigned, so no save can be loaded from this screen.", this);
    }

    private static TMP_Text Hook(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return null;
        button.onClick.AddListener(action);
        return button.GetComponentInChildren<TMP_Text>(true);
    }

    // Called by UIScreen every time this screen opens, so the slots always show the newest saves.
    protected override void OnOpening()
    {
        CacheMarkerHome();
        CacheBackground();

        _selected = -1;
        _confirmUntil = -1f;

        BuildSlots();
        RefreshSelection(instant: true);
    }

    protected override void OnClosing()
    {
        _confirmUntil = -1f;
    }

    // Hook this to the Back button's On Click.
    public void OnBackClicked()
    {
        if (UIManager.Instance != null) UIManager.Instance.Back();
    }

    private void Update()
    {
        if (!IsOpen) return;

        float dt = Time.unscaledDeltaTime;

        // Waited too long for the second Delete press: go back to normal.
        if (IsConfirmingDelete && Time.unscaledTime >= _confirmUntil)
        {
            _confirmUntil = -1f;
            RefreshButtons();
        }

        UpdateMarker(dt);
        UpdateBackground(dt);
    }

    // ---------- Slots ----------

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

        if (_hasSave.Length != slotUIs.Count)
        {
            _hasSave = new bool[slotUIs.Count];
            _images = new Sprite[slotUIs.Count];
            _marks = new SaveSlotSelectedMark[slotUIs.Count];
        }

        for (int i = 0; i < slotUIs.Count; i++)
        {
            _hasSave[i] = false;
            _images[i] = null;

            SaveSlotUI slot = slotUIs[i];
            if (slot == null) continue;

            // The glow that stays on the selected slot. Lives in its own component so SaveSlotUI stays untouched.
            if (_marks[i] == null) _marks[i] = slot.GetComponentInChildren<SaveSlotSelectedMark>(true);

            // More slot objects than save slots: hide the extra ones instead of breaking.
            bool inRange = SaveManager.IsValidSlot(i);
            slot.gameObject.SetActive(inRange);
            if (!inRange) continue;

            int slotIndex = i;
            SaveSlotMeta meta = manager.PeekMeta(slotIndex);
            bool hasSave = meta != null && meta.slotIndex == slotIndex;
            _hasSave[i] = hasSave;

            if (hasSave)
            {
                Sprite image = locationDatabase.GetImage(meta.locationId);
                _images[i] = image;

                slot.SetSlotData(
                    slotIndex,
                    FormatSaveTime(meta),
                    playTimePrefix + FormatPlayTime(meta.PlayTime),
                    locationDatabase.GetName(meta.locationId),
                    image);
            }
            else
            {
                slot.SetSlotData(slotIndex, "", "", emptyText, locationDatabase.FallbackImage);
            }

            // The bottom Delete button replaces the one on each slot.
            if (deleteButton != null && slot.ButtonDelete != null)
                slot.ButtonDelete.gameObject.SetActive(false);

            Button button = slot.Button;
            if (button == null)
            {
                Debug.LogWarning($"[LoadSaveScreen] Slot {slotIndex} has no Button.", slot);
                continue;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnSlotClicked(slotIndex));
        }
    }

    private void OnSlotClicked(int index)
    {
        // Clicking the selected slot again does nothing — loading is only from the Load / New Game button.
        if (index == _selected) return;

        _selected = index;
        _confirmUntil = -1f;
        RefreshSelection(instant: false);
    }

    // ---------- Bottom buttons ----------

    private void OnLoadClicked()
    {
        if (!HasSelection) return;
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) return;

        SaveManager manager = SaveManager.Instance;
        if (manager == null) return;

        if (_hasSave[_selected]) manager.LoadSave(_selected);
        else manager.NewGame(_selected);
    }

    private void OnDeleteClicked()
    {
        if (!HasSelection || !_hasSave[_selected]) return;

        // First press only asks. The second press within the time deletes.
        if (!IsConfirmingDelete)
        {
            _confirmUntil = Time.unscaledTime + deleteConfirmTime;
            RefreshButtons();
            return;
        }

        _confirmUntil = -1f;

        SaveManager manager = SaveManager.Instance;
        if (manager == null) return;

        manager.DeleteSave(_selected);
        BuildSlots();

        // The slot stays selected (now empty), so Load turns into New Game.
        RefreshSelection(instant: false);

        // Delete is grey now. Move keyboard / gamepad focus back to the slot so it isn't lost.
        EventSystem events = EventSystem.current;
        SaveSlotUI slot = slotUIs[_selected];
        if (events != null && events.currentSelectedGameObject == deleteButton.gameObject && slot.Button != null)
            events.SetSelectedGameObject(slot.Button.gameObject);
    }

    // ---------- Look ----------

    private void RefreshSelection(bool instant)
    {
        for (int i = 0; i < _marks.Length; i++)
        {
            if (_marks[i] != null) _marks[i].SetSelected(i == _selected, instant);
        }

        RefreshButtons();
        MoveMarkerTo(_selected, instant);

        if (backgroundFollowsSelection)
        {
            Sprite selectedImage = HasSelection
                ? (_hasSave[_selected] ? _images[_selected] : defaultBackground)
                : null;
            SetBackground(selectedImage, instant);
        }
    }

    private void RefreshButtons()
    {
        bool selected = HasSelection;
        bool hasSave = selected && _hasSave[_selected];

        if (loadButton != null) loadButton.interactable = selected;
        if (_loadText != null) _loadText.text = selected && !hasSave ? newGameLabel : loadLabel;

        if (deleteButton != null) deleteButton.interactable = hasSave;
        if (_deleteText != null) _deleteText.text = IsConfirmingDelete ? confirmDeleteLabel : deleteLabel;
    }

    // ---------- Marker ----------

    private void CacheMarkerHome()
    {
        if (marker == null || _markerHomeSet) return;
        _markerHome = marker.localPosition;
        _markerHomeSet = true;
    }

    // Nothing selected = back to where it was placed in the editor.
    private void MoveMarkerTo(int index, bool instant)
    {
        if (marker == null) return;

        _markerY = _markerHome.y;

        if (index >= 0 && index < slotUIs.Count && slotUIs[index] != null && marker.parent != null)
        {
            RectTransform slotRect = (RectTransform)slotUIs[index].transform;
            Vector3 slotCenter = slotRect.TransformPoint(slotRect.rect.center);
            float centerY = marker.parent.InverseTransformPoint(slotCenter).y;

            // localPosition is the pivot, so shift by the pivot-to-center distance.
            _markerY = centerY - (0.5f - marker.pivot.y) * marker.rect.height;
        }

        if (instant || markerSpeed <= 0f) SetMarkerY(_markerY);
    }

    private void UpdateMarker(float dt)
    {
        if (marker == null) return;

        float y = marker.localPosition.y;
        if (Mathf.Abs(y - _markerY) < 0.01f) return;

        if (markerSpeed <= 0f)
        {
            SetMarkerY(_markerY);
            return;
        }

        float t = 1f - Mathf.Exp(-markerSpeed * dt);
        SetMarkerY(Mathf.Lerp(y, _markerY, t));
    }

    private void SetMarkerY(float y)
    {
        Vector3 position = marker.localPosition;
        position.y = y;
        marker.localPosition = position;
    }

    // ---------- Background ----------

    private void CacheBackground()
    {
        if (background == null || _bgCached) return;

        _bgDefaultSprite = background.sprite;
        _bgDefaultColor = background.color;
        _bgBaseColor = _bgDefaultColor;
        _bgCached = true;
    }

    private void SetBackground(Sprite sprite, bool instant)
    {
        if (background == null) return;
        if (sprite == null) sprite = _bgDefaultSprite;

        if (background.sprite == sprite)
        {
            if (instant) { _bgAlpha = 1f; ApplyBackgroundColor(); }
            return;
        }

        background.sprite = sprite;

        // A location picture shows in its own colors. The default keeps the Inspector color (e.g. plain grey).
        _bgBaseColor = sprite == _bgDefaultSprite ? _bgDefaultColor : Color.white;
        _bgAlpha = instant ? 1f : 0f;
        ApplyBackgroundColor();
    }

    private void UpdateBackground(float dt)
    {
        if (background == null || _bgAlpha >= 1f) return;

        _bgAlpha = backgroundFade <= 0f ? 1f : Mathf.MoveTowards(_bgAlpha, 1f, dt / backgroundFade);
        ApplyBackgroundColor();
    }

    private void ApplyBackgroundColor()
    {
        Color color = _bgBaseColor;
        color.a *= _bgAlpha;
        background.color = color;
    }

    // ---------- Text ----------

    // InvariantCulture: on a Thai Windows the normal ToString writes the Buddhist year (2569).
    private string FormatSaveTime(SaveSlotMeta meta)
    {
        if (!meta.HasSaveTime) return datePrefix + noDateText;

        try
        {
            return datePrefix + meta.SavedAtLocal.ToString(saveTimeFormat, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[LoadSaveScreen] Save Time Format '{saveTimeFormat}' is not valid.", this);
            return datePrefix + meta.SavedAtLocal.ToString(CultureInfo.InvariantCulture);
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