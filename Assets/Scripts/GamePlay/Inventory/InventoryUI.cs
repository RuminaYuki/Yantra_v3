using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Put this on an object that is always active (e.g. the Canvas).
// "panel" is the child that gets shown and hidden.
//
// Left click  = select slot -> details + 3D preview on the left (stays until changed)
// Right click = select slot + open action menu (Drop, ...)
// Left drag   = move / merge / swap between slots
public class InventoryUI : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private PlayerInventory player;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform slotContainer; // add a GridLayoutGroup here
    [SerializeField] private SlotUI slotPrefab;
    [SerializeField] private ItemDetailPanel detailPanel;
    [SerializeField] private ItemContextMenu contextMenu;
    [SerializeField] private AmountPopup amountPopup;
    [Tooltip("Image that follows the mouse while dragging. Should be the last child of the panel.")]
    [SerializeField] private Image dragIcon;
    [SerializeField] private PlayerCameraController cameraController;

    [Header("State Machine (optional)")]
    [Tooltip("StateFlags on the player's StateMachineController object.")]
    [SerializeField] private StateFlagsAccess stateFlags;
    [Tooltip("Set to true while the inventory is open, false when closed.")]
    [SerializeField] private FlagSO inventoryOpenFlag;

    [Header("Actions")]
    [Tooltip("Actions available for every item (e.g. Drop). Category and item actions are added after these.")]
    [SerializeField] private List<ItemActionSO> globalActions = new List<ItemActionSO>();

    private SlotUI[] slotUIs;
    private int selectedIndex = -1;
    private int dragFromIndex = -1;
    private Canvas rootCanvas;

    public bool IsOpen => panel.activeSelf;

    private void Start()
    {
        Inventory inv = player.Inventory;
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;

        slotUIs = new SlotUI[inv.SlotCount];
        for (int i = 0; i < slotUIs.Length; i++)
        {
            slotUIs[i] = Instantiate(slotPrefab, slotContainer);
            slotUIs[i].Init(this, i);
            slotUIs[i].Refresh(inv[i]);
            slotUIs[i].SetSelected(false);
        }

        inv.OnSlotChanged += RefreshSlot;

        if (dragIcon != null)
        {
            dragIcon.raycastTarget = false; // must not block the drop target under the mouse
            dragIcon.gameObject.SetActive(false);
        }

        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (player == null || player.Inventory == null) return;
        player.Inventory.OnSlotChanged -= RefreshSlot;
    }

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        // Always start clean: nothing selected, no menu, no popup.
        CancelDrag();
        CloseOverlays();
        ClearSelection();

        panel.SetActive(open);

        // Free the mouse while open and stop camera look.
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        if (cameraController != null) cameraController.IsLookLocked = open;

        // Tell the player state machine. It only reads the flag; inventory logic stays here.
        if (stateFlags != null && inventoryOpenFlag != null) stateFlags.Set(inventoryOpenFlag, open);
    }

    // ---------- Called by actions ----------

    public void RequestAmount(string title, ItemData item, int max, Action<int> onConfirm)
    {
        if (amountPopup == null) { onConfirm?.Invoke(max); return; }
        amountPopup.Open(title, item, max, onConfirm);
    }

    // ---------- Called by SlotUI ----------

    public void HandleClick(int index, PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left)
        {
            CloseOverlays();
            // Clicking an empty slot keeps the current details on screen.
            if (!player.Inventory[index].IsEmpty) Select(index);
        }
        else if (e.button == PointerEventData.InputButton.Right)
        {
            CloseOverlays();
            if (player.Inventory[index].IsEmpty) return;
            Select(index);
            OpenContextMenu(index, e.position);
        }
    }

    public void HandleBeginDrag(int index, PointerEventData e)
    {
        if (player.Inventory[index].IsEmpty) return;

        CloseOverlays();
        dragFromIndex = index;
        slotUIs[index].SetDragging(true);

        if (dragIcon != null)
        {
            dragIcon.sprite = player.Inventory[index].item.icon;
            dragIcon.gameObject.SetActive(true);
            dragIcon.transform.SetAsLastSibling();
            MoveDragIcon(e);
        }
    }

    public void HandleDrag(PointerEventData e)
    {
        if (dragFromIndex >= 0) MoveDragIcon(e);
    }

    // Called on the slot under the mouse, before HandleEndDrag.
    public void HandleDropOnSlot(int targetIndex)
    {
        if (dragFromIndex < 0 || targetIndex == dragFromIndex) return;

        int from = dragFromIndex;
        CancelDrag();
        player.Inventory.Move(from, targetIndex);
        Select(targetIndex); // selection follows the item that was dragged
    }

    public void HandleEndDrag() => CancelDrag();

    // ---------- Selection ----------

    private void Select(int index)
    {
        if (!player.Inventory.IsValid(index) || player.Inventory[index].IsEmpty)
        {
            ClearSelection();
            return;
        }

        if (selectedIndex != index)
        {
            if (selectedIndex >= 0) slotUIs[selectedIndex].SetSelected(false);
            selectedIndex = index;
            slotUIs[index].SetSelected(true);
        }

        if (detailPanel != null) detailPanel.Show(player.Inventory[index].item);
    }

    private void ClearSelection()
    {
        if (selectedIndex >= 0 && slotUIs != null) slotUIs[selectedIndex].SetSelected(false);
        selectedIndex = -1;
        if (detailPanel != null) detailPanel.Clear();
    }

    // ---------- Context menu ----------

    private void OpenContextMenu(int index, Vector2 screenPos)
    {
        if (contextMenu == null) return;

        var ctx = new ItemActionContext(player, this, index);
        var entries = new List<(string, Action)>();
        foreach (ItemActionSO action in CollectActions(player.Inventory[index].item))
        {
            if (!action.CanExecute(ctx)) continue;
            ItemActionSO a = action;
            entries.Add((a.Label, () => a.Execute(new ItemActionContext(player, this, index))));
        }

        contextMenu.Open(entries, screenPos);
    }

    // Global -> category -> item. Duplicates are skipped.
    private List<ItemActionSO> CollectActions(ItemData item)
    {
        var result = new List<ItemActionSO>();
        void AddRange(List<ItemActionSO> list)
        {
            if (list == null) return;
            foreach (ItemActionSO a in list)
                if (a != null && !result.Contains(a)) result.Add(a);
        }

        AddRange(globalActions);
        if (item.category != null) AddRange(item.category.actions);
        AddRange(item.extraActions);
        return result;
    }

    // ---------- Helpers ----------

    private void RefreshSlot(int index)
    {
        slotUIs[index].Refresh(player.Inventory[index]);

        if (index == selectedIndex)
        {
            if (player.Inventory[index].IsEmpty) ClearSelection();
            else if (detailPanel != null) detailPanel.Show(player.Inventory[index].item);
        }
    }

    private void CloseOverlays()
    {
        if (contextMenu != null) contextMenu.Close();
        if (amountPopup != null) amountPopup.Close();
    }

    private void CancelDrag()
    {
        if (dragFromIndex >= 0 && slotUIs != null) slotUIs[dragFromIndex].SetDragging(false);
        dragFromIndex = -1;
        if (dragIcon != null) dragIcon.gameObject.SetActive(false);
    }

    private void MoveDragIcon(PointerEventData e)
    {
        RectTransform canvasRect = rootCanvas != null ? (RectTransform)rootCanvas.transform : (RectTransform)dragIcon.canvas.transform;
        Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, e.position, cam, out Vector3 world))
            dragIcon.transform.position = world;
    }
}
