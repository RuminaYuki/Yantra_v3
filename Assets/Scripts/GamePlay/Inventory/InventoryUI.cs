using UnityEngine;
using UnityEngine.EventSystems;

// Put this on an object that is always active (e.g. the Canvas).
// "panel" is the child that gets shown and hidden.
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory player;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform slotContainer; // add a GridLayoutGroup here
    [SerializeField] private SlotUI slotPrefab;
    [SerializeField] private TooltipUI tooltip;
    [SerializeField] private PlayerCameraController cameraController;

    private SlotUI[] slotUIs;
    private int hoveredIndex = -1;
    private int selectedIndex = -1; // slot picked for a click-to-move

    public bool IsOpen => panel.activeSelf;

    private void Start()
    {
        Inventory inv = player.Inventory;

        slotUIs = new SlotUI[inv.SlotCount];
        for (int i = 0; i < slotUIs.Length; i++)
        {
            slotUIs[i] = Instantiate(slotPrefab, slotContainer);
            slotUIs[i].Init(this, i);
            slotUIs[i].Refresh(inv[i]);
        }

        inv.OnSlotChanged += RefreshSlot;
        player.OnEquippedChanged += RefreshEquipped;
        RefreshEquipped(player.EquippedSlot);

        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (player == null || player.Inventory == null) return;
        player.Inventory.OnSlotChanged -= RefreshSlot;
        player.OnEquippedChanged -= RefreshEquipped;
    }

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        panel.SetActive(open);
        if (!open) { ClearSelection(); tooltip.Hide(); hoveredIndex = -1; }

        // Free the mouse while open. Also disable camera look in your camera script.
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        if (cameraController != null) cameraController.IsLookLocked = open;
    }

    // ---------- Called by SlotUI ----------

    public void HandleHoverEnter(int index)
    {
        hoveredIndex = index;
        ShowTooltipFor(index);
    }

    public void HandleHoverExit(int index)
    {
        if (hoveredIndex == index) hoveredIndex = -1;
        tooltip.Hide();
    }

    public void HandleClick(int index, PointerEventData.InputButton button)
    {
        if (button == PointerEventData.InputButton.Right)
        {
            player.ToggleEquip(index);
            return;
        }

        if (button != PointerEventData.InputButton.Left) return;

        if (selectedIndex == -1)
        {
            // First click: pick a non-empty slot
            if (!player.Inventory[index].IsEmpty)
            {
                selectedIndex = index;
                slotUIs[index].SetSelected(true);
            }
        }
        else
        {
            // Second click: move/merge/swap into the clicked slot
            player.Inventory.Move(selectedIndex, index);
            ClearSelection();
        }
    }

    // ---------- Refresh ----------

    private void RefreshSlot(int index)
    {
        slotUIs[index].Refresh(player.Inventory[index]);
        if (index == hoveredIndex) ShowTooltipFor(index);
    }

    private void RefreshEquipped(int equippedSlot)
    {
        for (int i = 0; i < slotUIs.Length; i++)
            slotUIs[i].SetEquipped(i == equippedSlot);
    }

    private void ShowTooltipFor(int index)
    {
        ItemStack s = player.Inventory[index];
        if (s.IsEmpty) tooltip.Hide();
        else tooltip.Show(s.item, slotUIs[index].Rect);
    }

    private void ClearSelection()
    {
        if (selectedIndex != -1 && slotUIs != null) slotUIs[selectedIndex].SetSelected(false);
        selectedIndex = -1;
    }
}
