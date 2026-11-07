using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Gamepad support for the inventory board (second half of InventoryUI).
//
// D-pad / left stick = move the cell cursor (skips over the rest of the item you are on)
// A                  = pick up the item under the cursor / place it (stays held if the spot is invalid)
// B                  = put the held item back / close the action menu or amount popup
// X                  = action menu for the item under the cursor (navigate with D-pad, A to choose)
// Rotate action      = turn the held item (gamepad binding: Y)
//
// Gamepad mode turns on with any gamepad input and off when the mouse moves or clicks,
// so mouse and gamepad can be mixed freely.
public partial class InventoryUI
{
    [Header("Gamepad")]
    [Tooltip("Optional. Cursor frame drawn over one cell (top-left pivot, resized to the cell). Empty = a simple outline is built at runtime.")]
    [SerializeField] private RectTransform padCursorPrefab;
    [SerializeField] private Color padCursorColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField, Min(1f)] private float padCursorThickness = 4f;
    [Tooltip("Stick tilt (0-1) needed to move the cursor.")]
    [SerializeField, Range(0.1f, 1f)] private float padStickThreshold = 0.5f;
    [Tooltip("Seconds before a held direction starts repeating.")]
    [SerializeField, Min(0f)] private float padRepeatDelay = 0.35f;
    [Tooltip("Seconds between repeats while a direction is held.")]
    [SerializeField, Min(0.01f)] private float padRepeatRate = 0.1f;

    private bool padMode;
    private bool padDragging;
    private Vector2Int padCursor;
    private Vector2Int padGrabOffset;
    private RectTransform padCursorFrame;
    private Vector2Int padHeldDir;
    private float padNextRepeat;
    private int padOverlayFrame = -10;

    private void Update()
    {
        if (!IsOpen || inv == null) return;

        if (UpdatePadMode()) return; // the input that switched modes does nothing else

        Gamepad pad = Gamepad.current;
        if (pad == null) return;

        if (IsOverlayOpen())
        {
            padOverlayFrame = Time.frameCount;
            if (pad.buttonEast.wasPressedThisFrame) CloseOverlays();
            else if (padMode) EnsureOverlaySelection();
            return;
        }

        // The A press that chose a menu entry must not also hit the board.
        if (Time.frameCount - padOverlayFrame <= 1) return;
        if (!padMode) return;

        Vector2Int step = ReadPadStep(pad);
        if (step != Vector2Int.zero) MovePadCursor(step);

        if (pad.buttonSouth.wasPressedThisFrame) PadSubmit();
        else if (pad.buttonEast.wasPressedThisFrame) { if (padDragging) CancelDrag(); }
        else if (pad.buttonWest.wasPressedThisFrame) PadOpenContextMenu();
    }

    // ---------- Mode switching ----------

    // Returns true if the mode changed this frame.
    private bool UpdatePadMode()
    {
        Gamepad pad = Gamepad.current;
        Mouse mouse = Mouse.current;

        if (!padMode && pad != null && PadHasInput(pad))
        {
            SetPadMode(true);
            return true;
        }

        if (padMode && mouse != null && MouseHasInput(mouse))
        {
            SetPadMode(false);
            return true;
        }

        return false;
    }

    private bool PadHasInput(Gamepad pad)
    {
        return pad.dpad.ReadValue().sqrMagnitude > 0.25f ||
               pad.leftStick.ReadValue().magnitude >= padStickThreshold ||
               pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
               pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame;
    }

    private static bool MouseHasInput(Mouse mouse)
    {
        return mouse.delta.ReadValue().sqrMagnitude > 4f ||
               mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
    }

    private void SetPadMode(bool value)
    {
        if (padMode == value) return;
        padMode = value;

        // A drag from the other device does not carry over.
        if (dragging != null && padDragging != value) CancelDrag();

        if (padMode)
        {
            if (selected != null && inv.Contains(selected)) padCursor = selected.Origin;
            ClampPadCursor();
            PadSelectUnderCursor();
        }

        if (IsOpen) Cursor.visible = !padMode;
        RefreshPadCursor();
    }

    // Called from SetOpen.
    private void OnPadOpenChanged(bool open)
    {
        padHeldDir = Vector2Int.zero;
        if (open)
        {
            ClampPadCursor();
            if (padMode)
            {
                Cursor.visible = false;
                PadSelectUnderCursor();
            }
        }
        RefreshPadCursor();
    }

    // ---------- Cursor ----------

    private Vector2Int ReadPadStep(Gamepad pad)
    {
        Vector2 v = pad.dpad.ReadValue();
        Vector2 stick = pad.leftStick.ReadValue();
        if (stick.sqrMagnitude > v.sqrMagnitude) v = stick;

        Vector2Int dir = Vector2Int.zero;
        if (v.magnitude >= padStickThreshold)
        {
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) dir.x = v.x > 0f ? 1 : -1;
            else dir.y = v.y > 0f ? -1 : 1; // board y goes down
        }

        float now = Time.unscaledTime;
        if (dir == Vector2Int.zero)
        {
            padHeldDir = Vector2Int.zero;
            return Vector2Int.zero;
        }

        if (dir != padHeldDir)
        {
            padHeldDir = dir;
            padNextRepeat = now + padRepeatDelay;
            return dir;
        }

        if (now >= padNextRepeat)
        {
            padNextRepeat = now + padRepeatRate;
            return dir;
        }

        return Vector2Int.zero;
    }

    private void MovePadCursor(Vector2Int step)
    {
        // When not holding anything, jump past the other cells of the item under the cursor.
        PlacedItem start = dragging == null ? ItemAtCell(padCursor) : null;
        Vector2Int c = padCursor;
        do
        {
            Vector2Int next = c + step;
            if (!inv.InBounds(next)) break;
            c = next;
        }
        while (start != null && ItemAtCell(c) == start);

        if (c == padCursor) return;
        padCursor = c;
        RefreshPadCursor();

        if (dragging != null) UpdatePadDrag();
        else PadSelectUnderCursor();
    }

    private void ClampPadCursor()
    {
        if (inv.InBounds(padCursor)) return;

        // Start on the first storage cell.
        for (int y = 0; y < inv.Height; y++)
            for (int x = 0; x < inv.Width; x++)
            {
                var c = new Vector2Int(x, y);
                if (inv.IsStorageCell(c)) { padCursor = c; return; }
            }
        padCursor = Vector2Int.zero;
    }

    private PlacedItem ItemAtCell(Vector2Int c) => inv.GetItemAt(c) ?? inv.GetBagAt(c);

    private void PadSelectUnderCursor()
    {
        PlacedItem p = ItemAtCell(padCursor);
        if (p != null) Select(p);
        else ClearSelection();
    }

    // ---------- Pick up / place ----------

    private void PadSubmit()
    {
        if (dragging == null)
        {
            PlacedItem p = ItemAtCell(padCursor);
            if (p == null) return;

            CloseOverlays();
            dragging = p;
            padDragging = true;
            dragRotation = p.Rotation;
            padGrabOffset = padCursor - p.Origin;
            if (views.TryGetValue(p, out ItemViewUI view)) view.SetDragging(true);

            ghost.gameObject.SetActive(true);
            UpdatePadDrag();
            return;
        }

        if (!padDragging) return;

        PlacedItem item = dragging;
        PlacedItem mergeTarget = FindMergeTargetAtCell(padCursor, item);

        // Invalid spot: keep holding so the player can move or rotate it.
        if (mergeTarget == null && !inv.TryMove(item, dragOrigin, dragRotation, simulate: true)) return;

        Vector2Int origin = dragOrigin;
        int rotation = dragRotation;
        CancelDrag();

        if (mergeTarget != null && inv.TryMerge(item, mergeTarget))
        {
            Select(mergeTarget);
            return;
        }

        inv.TryMove(item, origin, rotation);
        if (inv.Contains(item)) Select(item);
    }

    private void UpdatePadDrag()
    {
        ItemData item = dragging.Item;
        Vector2Int size = item.GetSize(dragRotation);

        // Keep the grabbed cell under the cursor (clamped if a rotation made the item smaller that way).
        var offset = new Vector2Int(
            Mathf.Clamp(padGrabOffset.x, 0, size.x - 1),
            Mathf.Clamp(padGrabOffset.y, 0, size.y - 1));

        dragOrigin = padCursor - offset;
        dragOverBoard = true;

        ghost.Show(item, dragging.Quantity, dragRotation, cellSize);
        ghost.SetTopLeft((Vector2)dragOrigin * cellSize);

        ClearHighlight();
        bool valid = FindMergeTargetAtCell(padCursor, dragging) != null ||
                     inv.TryMove(dragging, dragOrigin, dragRotation, simulate: true);
        SlotUI.Highlight h = valid ? SlotUI.Highlight.Valid : SlotUI.Highlight.Invalid;
        foreach (Vector2Int local in item.GetCells(dragRotation))
        {
            Vector2Int c = dragOrigin + local;
            if (!inv.InBounds(c)) continue;
            cells[c.x, c.y].SetHighlight(h);
            highlighted.Add(c);
        }
    }

    // Same rule as FindMergeTarget, but by cell instead of screen position.
    private PlacedItem FindMergeTargetAtCell(Vector2Int cell, PlacedItem p)
    {
        if (p.IsBag || p.Item.maxStack <= 1) return null;
        PlacedItem o = inv.GetItemAt(cell);
        return o != null && o != p && o.Item == p.Item && o.SpaceLeft > 0 ? o : null;
    }

    // ---------- Action menu ----------

    private void PadOpenContextMenu()
    {
        if (dragging != null || contextMenu == null) return;

        PlacedItem p = ItemAtCell(padCursor);
        if (p == null) return;

        Select(p);
        OpenContextMenu(p, CellScreenTopRight(padCursor));
        if (contextMenu.IsOpen) contextMenu.SelectFirst();
    }

    private bool IsOverlayOpen() =>
        (contextMenu != null && contextMenu.IsOpen) || (amountPopup != null && amountPopup.IsOpen);

    // A menu opened with the mouse has nothing selected; give the gamepad something to navigate from.
    private void EnsureOverlaySelection()
    {
        EventSystem es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject != null) return;

        if (amountPopup != null && amountPopup.IsOpen) amountPopup.SelectDefault();
        else if (contextMenu != null && contextMenu.IsOpen) contextMenu.SelectFirst();
    }

    private Vector2 CellScreenTopRight(Vector2Int cell)
    {
        Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        var corners = new Vector3[4];
        cells[cell.x, cell.y].Rect.GetWorldCorners(corners);
        return RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
    }

    // ---------- Cursor visual ----------

    private void BuildPadCursor()
    {
        if (padCursorPrefab != null)
        {
            padCursorFrame = Instantiate(padCursorPrefab, ghostLayer);
            foreach (Graphic g in padCursorFrame.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }
        else
        {
            var go = new GameObject("PadCursor", typeof(RectTransform));
            padCursorFrame = (RectTransform)go.transform;
            padCursorFrame.SetParent(ghostLayer, false);

            // Four thin edges make an outline that does not hide the item inside.
            CreateEdge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, padCursorThickness));
            CreateEdge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, padCursorThickness));
            CreateEdge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(padCursorThickness, 0f));
            CreateEdge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(padCursorThickness, 0f));
        }

        padCursorFrame.anchorMin = padCursorFrame.anchorMax = padCursorFrame.pivot = new Vector2(0f, 1f);
        padCursorFrame.sizeDelta = Vector2.one * cellSize;
        padCursorFrame.SetAsLastSibling(); // above the drag ghost
        padCursorFrame.gameObject.SetActive(false);
    }

    private void CreateEdge(string edgeName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        var go = new GameObject(edgeName, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padCursorFrame, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = padCursorColor;
        img.raycastTarget = false;
    }

    private void RefreshPadCursor()
    {
        if (padCursorFrame == null) return;

        bool show = padMode && IsOpen;
        padCursorFrame.gameObject.SetActive(show);
        if (!show) return;

        padCursorFrame.anchoredPosition = new Vector2(padCursor.x * cellSize, -padCursor.y * cellSize);
        padCursorFrame.SetAsLastSibling();
    }
}
