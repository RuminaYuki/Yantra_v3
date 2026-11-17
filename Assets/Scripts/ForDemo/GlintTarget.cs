using UnityEngine;

// ใส่ที่ของที่อยากให้มีแสงเหลื่อมแบบ RE4 แต่ไม่ใช่ WorldItem (เช่น ปืนบนโต๊ะ, ของในเควส)
// ItemGlintManager จะเจอแล้วใส่แสงให้ทุกโมเดลที่อยู่ใต้ตัวนี้ — ถูกซ่อนเมื่อไหร่แสงก็หายตาม
[DisallowMultipleComponent]
public class GlintTarget : MonoBehaviour
{
    private GameObject _glintOverlay;

    public void SetActiveGlint(bool value)
    {
        _glintOverlay.SetActive(value);
    }
    public void SetOverlay(GameObject overlay)
    {
        _glintOverlay = overlay;
    }
}
