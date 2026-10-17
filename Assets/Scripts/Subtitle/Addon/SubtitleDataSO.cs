using UnityEngine;

// โครงสร้างข้อมูล 1 ประโยค (ย่อหน้า)
[System.Serializable]
public struct SubtitleLine
{
    [Tooltip("ใครเป็นคนพูด? (ลากแฟ้ม SpeakerSO มาใส่ หรือเว้นว่างถ้าเป็นเสียงในหัว/เสียงบรรยาย)")]
    public SpeakerSO Speaker;

    [Header("Text & Localization")]
    [Tooltip("รหัสคีย์แปลภาษาของประโยคนี้ (เช่น scene01_sub_01)")]
    public string TextKey;

    [TextArea(2, 4)]
    [Tooltip("ข้อความซับไตเติ้ลเริ่มต้น (เอาไว้เทสตอนยังไม่ต่อระบบแปลภาษา)")]
    public string DefaultText;

    [Header("Audio & Timing")]
    [Tooltip("ไฟล์เสียงพากย์ (ถัามีไฟล์เสียง ระบบจะยึดเวลาโชว์ซับตามความยาวเสียงเป๊ะๆ)")]
    public AudioClip VoiceClip;

    // เพิ่มเข้ามาใหม่: ตัวตั้งเวลาสำรอง เผื่อบางประโยคเรายังอัดเสียงไม่เสร็จ หรือเป็นแค่ข้อความใบ้คำ
    [Tooltip("กรณีที่ไม่มี VoiceClip จะให้ซับประโยคนี้ค้างอยู่บนจอกี่วินาที?")]
    public float FallbackDuration;
}

[CreateAssetMenu(fileName = "NewSubtitleData", menuName = "YUKI Learning System/Subtitle/Subtitle Data")]
public class SubtitleDataSO : ScriptableObject
{
    [Tooltip("ลำดับบทพูดทั้งหมดในฉากนี้ (เล่นไล่จากบนลงล่าง)")]
    public SubtitleLine[] Lines;
}