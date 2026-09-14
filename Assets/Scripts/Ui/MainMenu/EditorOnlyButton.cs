using UnityEngine;
using Yantra.UI;

/// <summary>
/// ปิดตัวเองอัตโนมัติตอน build — ใช้กับของที่มีไว้ทดสอบเท่านั้น
///
/// แปะบนปุ่ม "Scene Select" ในเมนูหลัก
/// พอ build จริงปุ่มจะหายไปเอง คนเล่นไม่เห็น
/// ไม่ต้องจำลบก่อนส่งงาน
/// </summary>
public class EditorOnlyButton : MonoBehaviour
{
    [Tooltip("ให้ใช้งานได้ตอน build ด้วย\n" +
             "ติ๊กเฉพาะตอนอยากให้ทีมกดทดสอบใน build ที่ส่งกันเอง\n" +
             "อย่าลืมติ๊กออกก่อนส่งงานจริง")]
    [SerializeField] private bool _allowInBuild = false;

    private void Awake()
    {
#if !UNITY_EDITOR
        if (!_allowInBuild) gameObject.SetActive(false);
#endif
    }

    /// <summary>ผูกกับ OnClick ของปุ่ม — เปิดหน้าเลือก scene</summary>
    public void OnSceneSelectClicked()
    {
        UIManager.Instance?.Open(ScreenId.SceneSelect);
    }
}
