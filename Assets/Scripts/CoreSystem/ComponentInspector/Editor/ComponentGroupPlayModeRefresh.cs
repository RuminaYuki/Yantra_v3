using UnityEditor;
using UnityEngine;

// [InitializeOnLoad] = ให้ Unity รันโค้ดใน constructor นี้เองตอนเปิดโปรเจกต์/คอมไพล์เสร็จ
[InitializeOnLoad]
static class ComponentGroupPlayModeRefresh
{
    static ComponentGroupPlayModeRefresh()
    {
        // ฝากฟังก์ชันไว้กับ Unity: "ทุกครั้งที่ Play mode เปลี่ยนสถานะ ให้เรียกฉันด้วย"
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode &&
            state != PlayModeStateChange.EnteredPlayMode)
            return;

        // รอ Unity โหลดฉากเสร็จก่อน
        EditorApplication.delayCall += () =>
        {
            Object[] selected = Selection.objects;   // จำว่าตอนนี้เลือกอะไรอยู่

            // ถ้าไม่มี GameObject ไหนที่เลือกอยู่มีที่คั่น → ไม่ต้องทำอะไร
            bool hasHeader = false;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go.GetComponent<ComponentGroupHeader>() != null)
                {
                    hasHeader = true;
                    break;
                }
            }
            if (!hasHeader) return;

            Selection.objects = new Object[0];       // ยกเลิกการเลือก (= คลิกที่อื่น)

            // รออีกจังหวะ ให้ Inspector ล้างหน้าเดิมทิ้งก่อน
            EditorApplication.delayCall += () =>
                Selection.objects = selected;        // เลือกกลับ (= คลิกกลับมา)
        };
    }
}
