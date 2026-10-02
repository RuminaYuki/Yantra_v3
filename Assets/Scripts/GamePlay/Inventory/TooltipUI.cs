using TMPro;
using UnityEngine;

// Add a CanvasGroup with "Blocks Raycasts" OFF on the tooltip root,
// otherwise it can steal the pointer and make hover flicker.
public class TooltipUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text categoryText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Vector2 offset = new Vector2(20f, -20f);

    private void Awake() => Hide();

    public void Show(ItemData item, RectTransform anchor)
    {
        if (item == null) { Hide(); return; }

        nameText.text = item.displayName;
        categoryText.text = item.category.ToString();
        descriptionText.text = item.description;

        // Anchored next to the slot, so no input polling is needed
        root.transform.position = (Vector2)anchor.position + offset;
        root.SetActive(true);
    }

    public void Hide() => root.SetActive(false);
}
