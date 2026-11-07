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

    [Header("Highlight Fill (optional)")]
    [Tooltip("Image ลูกที่ทาสีทับทั้งช่องตอนลากของผ่าน — ใส่เมื่อช่องเป็นรูปรอยเย็บที่ตรงกลางโปร่งใส\n" +
             "เว้นว่าง = ย้อมสี Background แบบเดิม")]
    [SerializeField] private Image highlightFill;
    [SerializeField] private Color validFillColor = new Color(1f, 0.78f, 0.35f, 0.25f);
    [SerializeField] private Color invalidFillColor = new Color(0.75f, 0.15f, 0.12f, 0.3f);

    private bool storage;
    private Highlight highlight;

    public RectTransform Rect => (RectTransform)transform;

    private void Awake()
    {
        // Cells are drawn above bags, so they must not steal clicks meant for the bag.
        if (background) background.raycastTarget = false;
        if (highlightFill) highlightFill.raycastTarget = false;
        Apply();
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
        Color normal = storage ? storageColor : lockedColor;
        Color highlightColor = highlight == Highlight.Valid ? validColor : invalidColor;

        if (background)
            background.color = highlight == Highlight.None ? normal : highlightColor;

        if (!highlightFill) return;

        highlightFill.enabled = highlight != Highlight.None;
        highlightFill.color = highlight == Highlight.Valid ? validFillColor : invalidFillColor;
    }
}