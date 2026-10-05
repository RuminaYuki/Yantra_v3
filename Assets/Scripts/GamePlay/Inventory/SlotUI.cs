using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One cell of the grid. Only forwards pointer events to InventoryUI.
// Left click = select, right click = action menu, left drag = move/merge/swap.
public class SlotUI : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject selectedMark;

    private InventoryUI owner;
    private int index;

    public RectTransform Rect => (RectTransform)transform;

    public void Init(InventoryUI owner, int index)
    {
        this.owner = owner;
        this.index = index;
    }

    public void Refresh(ItemStack stack)
    {
        bool has = !stack.IsEmpty;
        icon.enabled = has;
        icon.sprite = has ? stack.item.icon : null;
        countText.text = has && stack.quantity > 1 ? stack.quantity.ToString() : string.Empty;
    }

    public void SetSelected(bool value) { if (selectedMark) selectedMark.SetActive(value); }

    // Dims the icon while it is being dragged.
    public void SetDragging(bool value)
    {
        Color c = icon.color;
        c.a = value ? 0.4f : 1f;
        icon.color = c;
    }

    // Unity does not send a click after a drag, so these never conflict.
    public void OnPointerClick(PointerEventData e) => owner.HandleClick(index, e);

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) owner.HandleBeginDrag(index, e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) owner.HandleDrag(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) owner.HandleEndDrag();
    }

    public void OnDrop(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) owner.HandleDropOnSlot(index);
    }
}
