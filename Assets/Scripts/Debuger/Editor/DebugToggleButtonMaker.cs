using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// เลือก Button ที่ใช้เป็นต้นแบบ → คลิกขวา / เมนู GameObject > UI > Debug > Toggle Button (จากปุ่มที่เลือก)
// จะได้ปุ่มหน้าตาเดียวกัน แต่เป็น Toggle: กดแล้วเทา (เปิด) กดอีกทีกลับปกติ (ปิด)
// ไม่ผูกฟังก์ชันให้ — ไปตั้ง On Value Changed เองว่าจะเรียกอะไร (Dynamic bool)
// ใช้ได้ทั้งในฉากและใน Prefab Mode, Ctrl+Z ได้
public static class DebugToggleButtonMaker
{
    private const string MenuPath = "GameObject/UI/Debug/Toggle Button (จากปุ่มที่เลือก)";
    private const float Gap = 20f;
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.5f);

    [MenuItem(MenuPath, true)]
    private static bool Validate()
    {
        return Selection.activeGameObject != null
            && Selection.activeGameObject.GetComponent<Button>() != null;
    }

    [MenuItem(MenuPath, false, 10)]
    private static void Create()
    {
        GameObject source = Selection.activeGameObject;
        Button sourceButton = source.GetComponent<Button>();

        GameObject go = Object.Instantiate(source, source.transform.parent);
        go.name = source.name + " Toggle";
        go.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
        Undo.RegisterCreatedObjectUndo(go, "Create Toggle Button");

        // วางข้างขวาปุ่มต้นแบบ (ถ้าอยู่ใน Layout Group ตำแหน่งนี้จะถูกจัดใหม่เองอยู่แล้ว)
        var rt = (RectTransform)go.transform;
        var srcRt = (RectTransform)source.transform;
        rt.anchoredPosition = srcRt.anchoredPosition
            + new Vector2(srcRt.rect.width * srcRt.localScale.x + Gap, 0f);

        // ---------- Button → Toggle (ยกค่าหน้าตาเดิมมา, ไม่เอา OnClick เดิมมา) ----------
        Image background = go.GetComponent<Image>();
        Object.DestroyImmediate(go.GetComponent<Button>());

        Toggle toggle = go.AddComponent<Toggle>();
        toggle.transition = sourceButton.transition;
        toggle.colors = sourceButton.colors;
        toggle.spriteState = sourceButton.spriteState;
        toggle.navigation = sourceButton.navigation;
        toggle.targetGraphic = background;

        // ---------- Overlay สีเทา = "Graphic" ของ Toggle (โชว์ตอนเปิด) ----------
        toggle.graphic = CreateOverlay(go.transform, background);
        toggle.isOn = false;

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    private static Image CreateOverlay(Transform parent, Image background)
    {
        var overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGo.layer = parent.gameObject.layer;

        var overlayRt = (RectTransform)overlayGo.transform;
        overlayRt.SetParent(parent, false);
        overlayRt.SetAsFirstSibling();   // อยู่ใต้ตัวหนังสือ
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlay = overlayGo.GetComponent<Image>();
        if (background != null)
        {
            // ใช้ sprite เดียวกับพื้นปุ่ม มุมโค้งจะได้ตรงกัน
            overlay.sprite = background.sprite;
            overlay.type = background.type;
            overlay.pixelsPerUnitMultiplier = background.pixelsPerUnitMultiplier;
        }
        overlay.color = OverlayColor;
        overlay.raycastTarget = false;
        return overlay;
    }
}
