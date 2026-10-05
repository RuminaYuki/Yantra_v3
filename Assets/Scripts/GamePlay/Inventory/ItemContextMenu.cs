using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Right-click menu. Put this on a full-screen transparent Image (raycast ON)
// that acts as a click blocker: clicking anywhere outside the menu closes it.
//
//   ContextMenu (this, full-screen Image, alpha 0)   <- starts inactive
//     Menu (menuRect: pivot (0,1), VerticalLayoutGroup + ContentSizeFitter)
//
// Buttons are spawned from buttonPrefab (a Button with a TMP_Text child).
public class ItemContextMenu : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform menuRect;
    [SerializeField] private Button buttonPrefab;

    private readonly List<Button> spawned = new List<Button>();

    public bool IsOpen => gameObject.activeSelf;

    public void Open(IReadOnlyList<(string label, Action onClick)> entries, Vector2 screenPos)
    {
        Clear();
        if (entries == null || entries.Count == 0) { Close(); return; }

        foreach (var entry in entries)
        {
            Button b = Instantiate(buttonPrefab, menuRect);
            TMP_Text t = b.GetComponentInChildren<TMP_Text>();
            if (t) t.text = entry.label;

            Action cb = entry.onClick;
            b.onClick.AddListener(() =>
            {
                Close();          // close first so the action can open another popup
                cb?.Invoke();
            });
            spawned.Add(b);
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        PlaceAt(screenPos);
    }

    public void Close()
    {
        Clear();
        gameObject.SetActive(false);
    }

    // Click on the blocker (anywhere outside a button) closes the menu.
    public void OnPointerClick(PointerEventData e) => Close();

    private void Clear()
    {
        foreach (Button b in spawned) if (b) Destroy(b.gameObject);
        spawned.Clear();
    }

    private void PlaceAt(Vector2 screenPos)
    {
        RectTransform parent = (RectTransform)menuRect.parent;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, cam, out Vector2 local);

        // Size is only known after layout, so rebuild now and keep the menu on screen.
        LayoutRebuilder.ForceRebuildLayoutImmediate(menuRect);
        Rect area = parent.rect;
        Vector2 size = menuRect.rect.size;
        Vector2 p = menuRect.pivot;

        local.x = Mathf.Clamp(local.x, area.xMin + size.x * p.x, area.xMax - size.x * (1f - p.x));
        local.y = Mathf.Clamp(local.y, area.yMin + size.y * p.y, area.yMax - size.y * (1f - p.y));
        menuRect.localPosition = local;
    }
}
