using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] Image imageBG;
    [SerializeField] TextMeshProUGUI slotNumber;
    [SerializeField] TextMeshProUGUI textSaveTime;
    [SerializeField] TextMeshProUGUI textPlayTime;
    [SerializeField] TextMeshProUGUI textLocation;

    //ตั้งค่า slotNumber, textTime, textLocation และ screenShot ตามข้อมูลที่ได้รับ
    public void SetSlotData(int slotIndex, string saveTime, string playtime, string location, Sprite screenshot)
    {
        slotNumber.text = $"Slot {slotIndex + 1}";
        textSaveTime.text = saveTime;
        textPlayTime.text = playtime;
        textLocation.text = location;
        imageBG.sprite = screenshot;
    }
}
