using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotPickUpLog : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text itemNameText;

    public void SetSlot(Sprite itemIcon, string itemName, int amount)
    {
        if (icon == null || amountText == null || itemNameText == null)
        {
            Debug.LogError("Icon, amount text, or item name text is not assigned on SlotPickUpLog.");
            return;
        }

        if (canvasGroup == null)
        {
            if (!TryGetComponent(out canvasGroup))
            {
                Debug.LogError("CanvasGroup component is missing on the SlotPickUpLog GameObject.");
                return;
            }
        }

        icon.sprite = itemIcon;
        icon.enabled = itemIcon != null;
        amountText.text = $"x{amount}";
        itemNameText.text = itemName;
        canvasGroup.alpha = 1f;
    }

    public void SetCanvasGroupAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }
}
