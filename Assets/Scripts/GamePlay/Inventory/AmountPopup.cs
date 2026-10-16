using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Modal popup that asks "how many?" (used by Drop, reusable for other actions).
// Put this on a full-screen dimmed Image (raycast ON) so the grid behind can't be clicked.
// Starts inactive.
public class AmountPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image icon;            // optional
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private Button minusButton;    // optional
    [SerializeField] private Button plusButton;     // optional
    [SerializeField] private Button maxButton;      // optional
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action<int> onConfirm;
    private int max = 1;
    private bool syncing;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(v => SetValue(Mathf.RoundToInt(v)));

        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.onEndEdit.AddListener(s => SetValue(int.TryParse(s, out int v) ? v : 1));

        if (minusButton) minusButton.onClick.AddListener(() => SetValue(Value - 1));
        if (plusButton) plusButton.onClick.AddListener(() => SetValue(Value + 1));
        if (maxButton) maxButton.onClick.AddListener(() => SetValue(max));

        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Close);
    }

    private int Value => Mathf.RoundToInt(slider.value);

    public void Open(string title, ItemData item, int maxAmount, Action<int> confirm)
    {
        gameObject.SetActive(true); // runs Awake the first time
        transform.SetAsLastSibling();

        onConfirm = confirm;
        max = Mathf.Max(1, maxAmount);

        if (titleText) titleText.text = title;
        if (icon)
        {
            icon.sprite = item != null ? item.icon : null;
            icon.enabled = icon.sprite != null;
        }

        slider.minValue = 1;
        slider.maxValue = max;
        SetValue(1);
    }

    public void Close()
    {
        onConfirm = null;
        gameObject.SetActive(false);
    }

    private void Confirm()
    {
        Action<int> cb = onConfirm;
        int amount = Value;
        Close();
        cb?.Invoke(amount);
    }

    private void SetValue(int v)
    {
        if (syncing) return;
        syncing = true;
        v = Mathf.Clamp(v, 1, max);
        slider.value = v;
        input.text = v.ToString();
        syncing = false;
    }
}
