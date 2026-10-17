using UnityEngine;

[CreateAssetMenu(fileName = "NewSpeaker", menuName = "YUKI Learning System/Subtitle/Speaker")]
public class SpeakerSO : ScriptableObject
{
    [Header("Speaker Info")]
    [Tooltip("รหัสคีย์แปลภาษาของชื่อตัวละคร (เช่น char_leon_name)")]
    public string SpeakerNameKey;

    [Tooltip("ชื่อเริ่มต้น (เอาไว้เทสตอนยังไม่ต่อระบบแปลภาษา)")]
    public string DefaultName;

    // เพิ่มเข้ามาใหม่: สีของชื่อตัวละคร (เช่น ตัวเอกสีขาว ผีสีแดง ช่วยให้คนเล่นแยกออกว่าใครพูด โดยไม่ต้องมีรูปหน้า)
    [Tooltip("สีของชื่อตัวละครเวลาโชว์ในซับไตเติ้ล")]
    public Color NameColor = Color.white;
}