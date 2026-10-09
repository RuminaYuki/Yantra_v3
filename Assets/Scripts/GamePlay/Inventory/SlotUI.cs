using UnityEngine;
using UnityEngine.UI;

// One cell of the board background. Visual only (no clicks):
// shows whether the cell is storage, and green/red while an item is dragged over it.
// Items are drawn by ItemViewUI on top of the cells.
public class SlotUI : MonoBehaviour
{
    public enum Highlight { None, Valid, Invalid }

    [SerializeField] private Image background;
    [SerializeField] private Color lockedColor = new Color(1f, 1f, 1f, 0.03f);
    [SerializeField] private Color storageColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color validColor = new Color(0.3f, 1f, 0.3f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

    private bool storage;
    private Highlight highlight;

    public RectTransform Rect => (RectTransform)transform;

    private void Awake()
    {
        // Cells are drawn above bags, so they must not steal clicks meant for the bag.
        if (background) background.raycastTarget = false;
    }

    public void SetStorage(bool value)
    {
        storage = value;
        Apply();
    }

    public void SetHighlight(Highlight value)
    {
        if (highlight == value) return;
        highlight = value;
        Apply();
    }

    private void Apply()
    {
        if (!background) return;
        background.color =
            highlight == Highlight.Valid ? validColor :
            highlight == Highlight.Invalid ? invalidColor :
            storage ? storageColor : lockedColor;
    }
}
