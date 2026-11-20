using TMPro;
using UnityEngine;

// ทำให้ตัวหนังสือ TextMeshPro อ่านออกบนฉากสว่าง — ขอบดำ + เงาฟุ้งดำ
// แปะบน GameObject เดียวกับ TextMeshPro (เช่น SubtitleText) — แก้เฉพาะตัวนี้ ไม่ไปแก้ฟอนต์ / material ที่ UI อื่นใช้ร่วม
// มีผลตอนกด Play (ใน Editor ตอนไม่ได้เล่นจะยังเห็นแบบเดิม)
[RequireComponent(typeof(TMP_Text))]
public class TMPReadable : MonoBehaviour
{
    [Header("ขอบ")]
    [Tooltip("0 = ไม่มี / 0.1-0.2 กำลังดี (ตัวหนังสืออ้วนขึ้นเท่าขอบ ขอบเลยอยู่ข้างนอก ไม่กินเนื้อจนตัวกลวง)")]
    [SerializeField, Range(0f, 0.5f)] private float _outline = 0.15f;
    [SerializeField] private Color _outlineColor = Color.black;

    [Header("เงาฟุ้ง")]
    [SerializeField] private bool _shadow = true;
    [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.9f);

    [Tooltip("เงาใหญ่แค่ไหน")]
    [SerializeField, Range(0f, 1f)] private float _shadowSize = 0.6f;

    [Tooltip("เงาฟุ้งแค่ไหน")]
    [SerializeField, Range(0f, 1f)] private float _shadowSoftness = 0.6f;

    [Tooltip("เงาเลื่อนลงเท่าไหร่ (ติดลบ = ลง)")]
    [SerializeField, Range(-1f, 1f)] private float _shadowOffsetY = -0.4f;

    private void Awake()
    {
        TMP_Text label = GetComponent<TMP_Text>();
        Material material = label.fontMaterial;   // ได้ material ของตัวนี้ตัวเดียว ไม่กระทบตัวอื่น

        if (_outline > 0f)
        {
            label.outlineWidth = _outline;
            label.outlineColor = _outlineColor;
            material.SetFloat(ShaderUtilities.ID_FaceDilate, _outline);
        }

        if (_shadow)
        {
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, _shadowColor);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, _shadowOffsetY);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, _shadowSize);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, _shadowSoftness);
        }

        label.UpdateMeshPadding();
    }
}
