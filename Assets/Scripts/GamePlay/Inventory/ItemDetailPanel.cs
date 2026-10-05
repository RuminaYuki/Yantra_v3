using TMPro;
using UnityEngine;

// Left side of the inventory: 3D preview, name, category, description.
// Shows the selected item and stays until the selection changes.
public class ItemDetailPanel : MonoBehaviour
{
    [Tooltip("Shown only when an item is selected (texts + preview).")]
    [SerializeField] private GameObject contentRoot;
    [Tooltip("Optional. Shown when nothing is selected, e.g. a 'Select an item' hint.")]
    [SerializeField] private GameObject emptyRoot;

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text categoryText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private ItemPreview3D preview;

    private ItemData current;

    private void Awake() => Clear();

    public void Show(ItemData item)
    {
        if (item == null) { Clear(); return; }

        if (contentRoot) contentRoot.SetActive(true);
        if (emptyRoot) emptyRoot.SetActive(false);

        nameText.text = string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
        categoryText.text = item.category != null ? item.category.DisplayName : string.Empty;
        descriptionText.text = item.description;

        // Only rebuild the model when the item actually changed (e.g. not on a quantity change).
        if (item != current && preview != null) preview.Show(item);
        current = item;
    }

    public void Clear()
    {
        current = null;
        if (contentRoot) contentRoot.SetActive(false);
        if (emptyRoot) emptyRoot.SetActive(true);

        nameText.text = string.Empty;
        categoryText.text = string.Empty;
        descriptionText.text = string.Empty;
        if (preview != null) preview.Clear();
    }
}
