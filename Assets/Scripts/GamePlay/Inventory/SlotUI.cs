using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject selectedMark;
    [SerializeField] private GameObject equippedMark;

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
    public void SetEquipped(bool value) { if (equippedMark) equippedMark.SetActive(value); }

    public void OnPointerEnter(PointerEventData e) => owner.HandleHoverEnter(index);
    public void OnPointerExit(PointerEventData e) => owner.HandleHoverExit(index);
    public void OnPointerClick(PointerEventData e) => owner.HandleClick(index, e.button);
}
