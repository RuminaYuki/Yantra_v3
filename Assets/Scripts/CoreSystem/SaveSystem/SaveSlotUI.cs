using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One save slot on the Load Save screen. Only shows data; LoadSaveScreen decides what it does.
public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] Image imageBG;
    [SerializeField] TextMeshProUGUI slotNumber;
    [SerializeField] TextMeshProUGUI textSaveTime;
    [SerializeField] TextMeshProUGUI textPlayTime;
    [SerializeField] TextMeshProUGUI textLocation;

    [Tooltip("Leave empty to use the Button on this object.")]
    [SerializeField] Button button;

    public Button Button => button != null ? button : (button = GetComponent<Button>());

    // Sets the slot number, save time, play time, location name and background image.
    public void SetSlotData(int slotIndex, string saveTime, string playTime, string location, Sprite locationImage)
    {
        if (slotNumber != null) slotNumber.text = $"Slot {slotIndex + 1}";
        if (textSaveTime != null) textSaveTime.text = saveTime;
        if (textPlayTime != null) textPlayTime.text = playTime;
        if (textLocation != null) textLocation.text = location;
        if (imageBG != null) imageBG.sprite = locationImage;
    }
}
