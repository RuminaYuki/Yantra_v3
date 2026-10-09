using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Put this on an object that is always active (e.g. the Canvas).
// "panel" is the child that gets shown and hidden.
//
// Left click  = select item -> details + 3D preview on the left (stays until changed)
// Right click = select item + open action menu (Drop, ...)
// Left drag   = move item (snaps to cells, green/red preview). Rotate key while dragging = turn 90 degrees.
//               Dropping a stackable item on the same item merges the stacks.
//
// gridRoot: an empty RectTransform with anchors NOT stretched (its size is set from the board).
// Layers are created under it at runtime: Bags -> Cells -> Items -> Ghost.
public class InventoryUI : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private PlayerInventory player;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform gridRoot;
    [SerializeField] private SlotUI cellPrefab;
    [SerializeField] private ItemViewUI itemViewPrefab;
    [SerializeField] private ItemDetailPanel detailPanel;
    [SerializeField] private ItemContextMenu contextMenu;
    [SerializeField] private AmountPopup amountPopup;
    [SerializeField] private PlayerCameraController cameraController;

    [Header("Board")]
    [SerializeField, Min(8f)] private float cellSize = 64f;

    [Header("Input")]
    [Tooltip("Button action (e.g. R). Rotates the dragged item 90 degrees clockwise.")]
    [SerializeField] private InputActionReference rotateAction;

    [Header("State Machine (optional)")]
    [Tooltip("StateFlags on the player's StateMachineController object.")]
    [SerializeField] private StateFlagsAccess stateFlags;
    [Tooltip("Set to true while the inventory is open, false when closed.")]
    [SerializeField] private FlagSO inventoryOpenFlag;

    [Header("Actions")]
    [Tooltip("Actions available for every item (e.g. Drop). Category and item actions are added after these.")]
    [SerializeField] private List<ItemActionSO> globalActions = new List<ItemActionSO>();

    private Inventory inv;
    private Canvas rootCanvas;
    private SlotUI[,] cells;
    private RectTransform bagLayer, cellLayer, itemLayer, ghostLayer;
    private readonly Dictionary<PlacedItem, ItemViewUI> views = new Dictionary<PlacedItem, ItemViewUI>();
    private PlacedItem selected;

    // Drag state
    private PlacedItem dragging;
    private int dragRotation;
    private Vector2Int dragOrigin;
    private bool dragOverBoard;
    private Vector2 lastPointer;
    private ItemViewUI ghost;
    private readonly List<Vector2Int> highlighted = new List<Vector2Int>();

    public bool IsOpen => panel.activeSelf;

    private void OnEnable()
    {
        if (rotateAction != null) rotateAction.action.performed += HandleRotate;
    }

    private void OnDisable()
    {
        if (rotateAction != null) rotateAction.action.performed -= HandleRotate;
    }

    private void Start()
    {
        inv = player.Inventory;
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;

        BuildBoard();
        foreach (PlacedItem p in inv.Items) CreateView(p);

        inv.ItemAdded += CreateView;
        inv.ItemRemoved += HandleItemRemoved;
        inv.ItemChanged += HandleItemChanged;
        inv.CellsChanged += RefreshCells;

        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (inv == null) return;
        inv.ItemAdded -= CreateView;
        inv.ItemRemoved -= HandleItemRemoved;
        inv.ItemChanged -= HandleItemChanged;
        inv.CellsChanged -= RefreshCells;
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

    // ---------- Called by ItemViewUI ----------

    public void HandleClick(ItemViewUI view, PointerEventData e)
    {
        PlacedItem p = view.Target;
        if (!inv.Contains(p)) return;

        CloseOverlays();
        Select(p);
        if (e.button == PointerEventData.InputButton.Right) OpenContextMenu(p, e.position);
    }

    public void HandleBeginDrag(ItemViewUI view, PointerEventData e)
    {
        if (!inv.Contains(view.Target)) return;

        CloseOverlays();
        dragging = view.Target;
        dragRotation = dragging.Rotation;
        view.SetDragging(true);

        ghost.gameObject.SetActive(true);
        UpdateDrag(e.position);
    }

    public void HandleDrag(PointerEventData e)
    {
        if (dragging != null) UpdateDrag(e.position);
    }

    public void HandleEndDrag(PointerEventData e)
    {
        if (dragging == null) return;

        UpdateDrag(e.position);
        PlacedItem p = dragging;
        PlacedItem mergeTarget = FindMergeTarget(e.position, p);
        bool overBoard = dragOverBoard;
        Vector2Int origin = dragOrigin;
        int rotation = dragRotation;
        CancelDrag();

        if (mergeTarget != null && inv.TryMerge(p, mergeTarget))
        {
            Select(mergeTarget);
            return;
        }

        // An invalid spot simply leaves the item where it was.
        if (overBoard) inv.TryMove(p, origin, rotation);
        if (inv.Contains(p)) Select(p); // selection follows the item that was dragged
    }

    // ---------- Drag ----------

    private void HandleRotate(InputAction.CallbackContext _)
    {
        if (dragging == null || !dragging.Item.canRotate) return;
        dragRotation = ItemData.WrapRotation(dragRotation + 1);
        UpdateDrag(lastPointer);
    }

    private void UpdateDrag(Vector2 screenPos)
    {
        lastPointer = screenPos;
        ItemData item = dragging.Item;

        // The ghost is centered on the mouse and snaps to the nearest cell.
        dragOverBoard = TryGetBoardPoint(screenPos, out Vector2 point);
        Vector2 size = (Vector2)item.GetSize(dragRotation) * cellSize;
        Vector2 topLeft = point - size * 0.5f;
        dragOrigin = new Vector2Int(Mathf.RoundToInt(topLeft.x / cellSize), Mathf.RoundToInt(topLeft.y / cellSize));

        ghost.Show(item, dragging.Quantity, dragRotation, cellSize);
        ghost.SetTopLeft(topLeft);

        ClearHighlight();
        if (!dragOverBoard) return;

        bool valid = FindMergeTarget(screenPos, dragging) != null || inv.CanMove(dragging, dragOrigin, dragRotation);
        SlotUI.Highlight h = valid ? SlotUI.Highlight.Valid : SlotUI.Highlight.Invalid;
        foreach (Vector2Int local in item.GetCells(dragRotation))
        {
            Vector2Int c = dragOrigin + local;
            if (!inv.InBounds(c)) continue;
            cells[c.x, c.y].SetHighlight(h);
            highlighted.Add(c);
        }
    }

    // Stackable item dropped on another stack of the same item that still has room.
    private PlacedItem FindMergeTarget(Vector2 screenPos, PlacedItem p)
    {
        if (p.IsBag || p.Item.maxStack <= 1) return null;
        if (!TryGetBoardPoint(screenPos, out Vector2 point)) return null;

        var cell = new Vector2Int(Mathf.FloorToInt(point.x / cellSize), Mathf.FloorToInt(point.y / cellSize));
        PlacedItem o = inv.GetItemAt(cell);
        return o != null && o != p && o.Item == p.Item && o.SpaceLeft > 0 ? o : null;
    }

    private void CancelDrag()
    {
        if (dragging != null && views.TryGetValue(dragging, out ItemViewUI v)) v.SetDragging(false);
        dragging = null;
        if (ghost != null) ghost.gameObject.SetActive(false);
        ClearHighlight();
    }

    private void ClearHighlight()
    {
        foreach (Vector2Int c in highlighted) cells[c.x, c.y].SetHighlight(SlotUI.Highlight.None);
        highlighted.Clear();
    }

    // Board pixels: x right, y down from the top-left corner. Returns false if outside the board.
    private bool TryGetBoardPoint(Vector2 screenPos, out Vector2 point)
    {
        Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        point = Vector2.zero;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, screenPos, cam, out Vector2 local))
            return false;

        Rect r = gridRoot.rect;
        point = new Vector2(local.x - r.xMin, r.yMax - local.y);
        return point.x >= 0f && point.y >= 0f && point.x < r.width && point.y < r.height;
    }

    // ---------- Selection ----------

    private void Select(PlacedItem p)
    {
        if (!inv.Contains(p))
        {
            ClearSelection();
            return;
        }

        if (selected != p)
        {
            if (selected != null && views.TryGetValue(selected, out ItemViewUI old)) old.SetSelected(false);
            selected = p;
            views[p].SetSelected(true);
        }

        if (detailPanel != null) detailPanel.Show(p.Item);
    }

    private void ClearSelection()
    {
        if (selected != null && views.TryGetValue(selected, out ItemViewUI v)) v.SetSelected(false);
        selected = null;
        if (detailPanel != null) detailPanel.Clear();
    }

    // ---------- Context menu ----------

    private void OpenContextMenu(PlacedItem p, Vector2 screenPos)
    {
        if (contextMenu == null) return;

        var ctx = new ItemActionContext(player, this, p);
        var entries = new List<(string, Action)>();
        foreach (ItemActionSO action in CollectActions(p.Item))
        {
            if (!action.CanExecute(ctx)) continue;
            ItemActionSO a = action;
            entries.Add((a.Label, () => a.Execute(new ItemActionContext(player, this, p))));
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

    // ---------- Board building / inventory events ----------

    private void BuildBoard()
    {
        gridRoot.sizeDelta = new Vector2(inv.Width, inv.Height) * cellSize;

        bagLayer = CreateLayer("Bags");
        cellLayer = CreateLayer("Cells");
        itemLayer = CreateLayer("Items");
        ghostLayer = CreateLayer("Ghost");

        cells = new SlotUI[inv.Width, inv.Height];
        for (int y = 0; y < inv.Height; y++)
            for (int x = 0; x < inv.Width; x++)
            {
                SlotUI cell = Instantiate(cellPrefab, cellLayer);
                RectTransform rt = cell.Rect;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = Vector2.one * cellSize;
                rt.anchoredPosition = new Vector2(x * cellSize, -y * cellSize);
                cells[x, y] = cell;
            }
        RefreshCells();

        ghost = Instantiate(itemViewPrefab, ghostLayer);
        ghost.MakeGhost();
        ghost.gameObject.SetActive(false);
    }

    private RectTransform CreateLayer(string layerName)
    {
        var go = new GameObject(layerName, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(gridRoot, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void RefreshCells()
    {
        for (int y = 0; y < inv.Height; y++)
            for (int x = 0; x < inv.Width; x++)
                cells[x, y].SetStorage(inv.IsStorageCell(new Vector2Int(x, y)));
    }

    private void CreateView(PlacedItem p)
    {
        ItemViewUI view = Instantiate(itemViewPrefab, p.IsBag ? bagLayer : itemLayer);
        view.Init(this, p);
        view.Show(p.Item, p.Quantity, p.Origin, p.Rotation, cellSize);
        view.SetSelected(false);
        views[p] = view;
    }

    private void HandleItemRemoved(PlacedItem p)
    {
        if (p == dragging) CancelDrag();
        if (p == selected) ClearSelection();
        if (views.TryGetValue(p, out ItemViewUI view))
        {
            Destroy(view.gameObject);
            views.Remove(p);
        }
    }

    private void HandleItemChanged(PlacedItem p)
    {
        if (views.TryGetValue(p, out ItemViewUI view))
            view.Show(p.Item, p.Quantity, p.Origin, p.Rotation, cellSize);
        if (p == selected && detailPanel != null) detailPanel.Show(p.Item);
    }

    private void CloseOverlays()
    {
        if (contextMenu != null) contextMenu.Close();
        if (amountPopup != null) amountPopup.Close();
    }
}
