using UnityEngine;

// บรรยากาศของพื้นที่หนึ่งชุด: แสงหลัก (Directional Light) + หมอก (Fog ใน Global Volume)
// สร้างไว้หลายอัน เช่น Atmosphere_Forest, Atmosphere_Village แล้วสั่งผ่าน AtmosphereZone / AtmosphereController
[CreateAssetMenu(fileName = "Atmosphere_", menuName = "Yantra/Atmosphere/Atmosphere Profile")]
public class AtmosphereProfileSO : ScriptableObject
{
    [Header("Light — Intensity")]
    [Tooltip("ความสว่าง หน่วย Lux (ช่อง Intensity ของ Directional Light)")]
    [Min(0f)] public float intensity = 9000f;

    [Header("Light — Filter")]
    [Tooltip("สี Filter ของไฟ (ช่อง Filter ใน Emission ของ Light)")]
    [ColorUsage(false)] public Color filter = Color.white;

    [Tooltip("ติ๊ก = ใช้ Temperature ด้วย (สีจริง = Filter x สีของอุณหภูมิ)")]
    public bool useTemperature = true;

    [Tooltip("อุณหภูมิสี หน่วย Kelvin — ต่ำ = ส้ม/แดง, สูง = ฟ้า")]
    [Range(1500f, 20000f)] public float temperature = 6500f;

    [Header("Fog (Global Volume)")]
    [Tooltip("ติ๊ก = เปลี่ยนหมอกด้วย / ไม่ติ๊ก = หมอกคงค่าเดิม")]
    public bool useFog = true;

    [Tooltip("Fog > Maximum Height — หมอกหนาถึงความสูงเท่าไหร่ (ตอนนี้ Level1 ตั้งไว้ 50)")]
    [Min(0f)] public float fogMaximumHeight = 50f;

    [Header("Transition")]
    [Tooltip("ใช้เวลากี่วินาทีเปลี่ยนจากค่าเดิมมาเป็นค่านี้ (0 = เปลี่ยนทันที)")]
    [Min(0f)] public float transitionTime = 2f;
}
