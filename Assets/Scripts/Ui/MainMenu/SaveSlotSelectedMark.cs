using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แสงค้างบนช่องเซฟที่ถูกเลือกอยู่ — LoadSaveScreen เป็นคนสั่งเปิด/ปิด
///
/// แปะบน Image "Selected" ข้างใน prefab SaveSlot
/// ความเข้มตอนติดเต็มที่ = ค่า A ของ Color ที่ตั้งใน Inspector
///
/// แยกเป็นไฟล์ของเราเอง จะได้ไม่ต้องไปแก้ SaveSlotUI ของเพื่อน
/// </summary>
[RequireComponent(typeof(Graphic))]
public class SaveSlotSelectedMark : MonoBehaviour
{
    [Tooltip("เวลาค่อย ๆ ติด / ค่อย ๆ ดับ (วินาที)")]
    [SerializeField, Min(0f)] private float _fadeTime = 0.12f;

    private Graphic _graphic;
    private float _fullAlpha = -1f;   // ค่า A ที่ตั้งไว้ใน Inspector อ่านครั้งเดียว
    private float _amount;            // 0..1 ตอนนี้ติดอยู่แค่ไหน
    private bool _selected;

    private void Awake()
    {
        Init();
    }

    public void SetSelected(bool selected, bool instant = false)
    {
        Init();
        _selected = selected;

        if (instant || _fadeTime <= 0f)
        {
            _amount = selected ? 1f : 0f;
            Apply();
        }
    }

    private void Update()
    {
        float target = _selected ? 1f : 0f;
        if (_amount == target) return;

        // unscaled — หน้านี้อาจเปิดตอน timeScale = 0
        _amount = Mathf.MoveTowards(_amount, target, Time.unscaledDeltaTime / Mathf.Max(_fadeTime, 0.0001f));
        Apply();
    }

    private void Init()
    {
        if (_fullAlpha >= 0f) return;

        _graphic = GetComponent<Graphic>();
        _fullAlpha = _graphic.color.a;
        _graphic.raycastTarget = false;   // ไม่บังคลิกของช่อง
        Apply();
    }

    private void Apply()
    {
        Color color = _graphic.color;
        color.a = _fullAlpha * _amount;
        _graphic.color = color;
    }
}
