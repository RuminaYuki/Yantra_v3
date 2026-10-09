using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Visual of one placed item (or bag) on the board. Its size follows the item's shape.
// Forwards pointer events to InventoryUI: left click = select, right click = action menu,
// left drag = move. Clicks on the empty corners of an L-shape fall through.
//
// Prefab:
//   ItemView (this, RectTransform)
//     Icon         (Image, raycast ON)   <- rotated, so keep it a child
//     SelectedMark (Image, stretched, raycast OFF, starts inactive)
//     Count        (TMP_Text, bottom-right, raycast OFF)
public class ItemViewUI : MonoBehaviour,
    ICanvasRaycastFilter, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject selectedMark;

    private InventoryUI owner;
    private ItemData item;
    private int rotation;
    private float cellSize = 1f;

    public PlacedItem Target { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    public void Init(InventoryUI owner, PlacedItem target)
    {
        this.owner = owner;
        Target = target;
    }

    // The drag ghost uses the same prefab but never receives clicks.
    public void MakeGhost()
    {
        owner = null;
        Target = null;
        foreach (Graphic g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        SetSelected(false);
    }

    // Draws an item snapped to a cell.
    public void Show(ItemData item, int quantity, Vector2Int origin, int rotation, float cellSize)
    {
        Show(item, quantity, rotation, cellSize);
        SetTopLeft(new Vector2(origin.x, origin.y) * cellSize);
    }

    // Draws an item without moving it (the ghost positions itself freely).
    public void Show(ItemData item, int quantity, int rotation, float cellSize)
    {
        this.item = item;
        this.rotation = rotation;
        this.cellSize = cellSize;

        RectTransform rt = Rect;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = (Vector2)item.GetSize(rotation) * cellSize;

        // The icon is laid out unrotated, then turned around its center.
        RectTransform ir = icon.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.anchoredPosition = Vector2.zero;
        ir.sizeDelta = (Vector2)item.GetSize(0) * cellSize;
        ir.localRotation = Quaternion.Euler(0f, 0f, -90f * rotation);

        icon.sprite = item.icon;
        icon.enabled = item.icon != null;
        if (countText) countText.text = quantity > 1 ? quantity.ToString() : string.Empty;
    }

    // Position in board pixels: x right, y down from the board's top-left corner.
    public void SetTopLeft(Vector2 boardPixel) => Rect.anchoredPosition = new Vector2(boardPixel.x, -boardPixel.y);

    public void SetSelected(bool value) { if (selectedMark) selectedMark.SetActive(value); }

    // Dims the item while it is being dragged.
    public void SetDragging(bool value)
    {
        Color c = icon.color;
        c.a = value ? 0.4f : 1f;
        icon.color = c;
    }

    // Only cells that are part of the shape can be clicked.
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (item == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screenPoint, eventCamera, out Vector2 local))
            return false;

        // Pivot is top-left, so local.y is negative going down.
        var cell = new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(-local.y / cellSize));
        foreach (Vector2Int c in item.GetCells(rotation))
            if (c == cell) return true;
        return false;
    }

    // Unity does not send a click after a drag, so these never conflict.
    public void OnPointerClick(PointerEventData e)
    {
        if (owner != null) owner.HandleClick(this, e);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (owner != null && e.button == PointerEventData.InputButton.Left) owner.HandleBeginDrag(this, e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (owner != null && e.button == PointerEventData.InputButton.Left) owner.HandleDrag(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (owner != null && e.button == PointerEventData.InputButton.Left) owner.HandleEndDrag(e);
    }
}
