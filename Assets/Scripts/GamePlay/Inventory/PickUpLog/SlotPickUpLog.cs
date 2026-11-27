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
        if (!HasRequiredReferences()) return;

        icon.sprite = itemIcon;
        icon.enabled = itemIcon != null;
        amountText.text = $"x{amount}";
        itemNameText.text = itemName;
        canvasGroup.alpha = 1f;
    }

    public void SetMessage(string message)
    {
        if (!HasRequiredReferences()) return;

        icon.sprite = null;
        icon.enabled = false;
        amountText.text = string.Empty;
        itemNameText.text = message;
        canvasGroup.alpha = 1f;
    }

    public void SetCanvasGroupAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }

    private bool HasRequiredReferences()
    {
        if (icon == null || amountText == null || itemNameText == null)
        {
            Debug.LogError("Icon, amount text, or item name text is not assigned on SlotPickUpLog.");
            return false;
        }

        if (canvasGroup == null && !TryGetComponent(out canvasGroup))
        {
            Debug.LogError("CanvasGroup component is missing on the SlotPickUpLog GameObject.");
            return false;
        }

        return true;
    }
}
