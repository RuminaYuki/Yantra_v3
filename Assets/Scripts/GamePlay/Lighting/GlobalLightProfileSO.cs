using UnityEngine;

// ค่าแสงหลักของฉาก (Directional Light) หนึ่งชุด — สร้างไว้หลายอัน เช่น Night_Normal, Night_Dark, Ritual_Red
// แล้วสั่ง GlobalLightController.ApplyProfile(profile) จาก Trigger Zone Events
[CreateAssetMenu(fileName = "GlobalLight_", menuName = "Yantra/Lighting/Global Light Profile")]
public class GlobalLightProfileSO : ScriptableObject
{
    [Header("Intensity")]
    [Tooltip("ความสว่าง หน่วย Lux (ช่อง Intensity ของ Directional Light) — ตอนนี้ Level1 ตั้งไว้ 9000")]
    [Min(0f)] public float intensity = 9000f;

    [Header("Filter")]
    [Tooltip("สี Filter ของไฟ (ช่อง Filter ใน Emission ของ Light)")]
    [ColorUsage(false)] public Color filter = Color.white;

    [Tooltip("ติ๊ก = ใช้ Temperature ด้วย (สีจริง = Filter x สีของอุณหภูมิ)")]
    public bool useTemperature = true;

    [Tooltip("อุณหภูมิสี หน่วย Kelvin — ต่ำ = ส้ม/แดง, สูง = ฟ้า")]
    [Range(1500f, 20000f)] public float temperature = 6500f;

    [Header("Transition")]
    [Tooltip("ใช้เวลากี่วินาทีเปลี่ยนจากค่าเดิมมาเป็นค่านี้ (0 = เปลี่ยนทันที)")]
    [Min(0f)] public float transitionTime = 2f;
}
