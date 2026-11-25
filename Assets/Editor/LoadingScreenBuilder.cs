#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// จัดหน้าโหลดตามไฟล์ Loading.psd ให้ในคลิกเดียว
/// เปิด prefab UIRoot ค้างไว้ (หรือเปิดฉากที่มี UIRoot) แล้วกด Tools > Yantra > Build Loading Screen
///
/// ทำอะไรบ้าง (กด Ctrl+Z ย้อนได้ทั้งหมด, กดซ้ำได้ ไม่สร้างซ้ำ):
/// - BG     : รูปพื้นหลังเต็มจอ (ยังไม่มีรูปในเกม = ปิดไว้ ใช้ VideoBG เดิมไปก่อน)
/// - Shade  : เงาดำไล่จากล่างขึ้นบน
/// - LoadingText / LineBG / LineFill : คำว่า Loading... กับเส้นโหลดด้านล่าง
/// - ต่อช่อง Background / Progress Fill / Loading Label ใน LoadingScreen ให้
/// - ปิด Spinner กับตัวเลข % (ในแบบไม่มี) — เปิดคืนได้โดยติ๊กเอง
/// </summary>
public static class LoadingScreenBuilder
{
    // ตำแหน่งจาก PSD 1920x1080 (จุดกึ่งกลาง วัดจากมุมซ้ายล่าง)
    private static readonly Vector2 TextPos = new Vector2(298f, 86.5f);
    private static readonly Vector2 LinePos = new Vector2(1119.5f, 85f);
    private const float LineBgAlpha = 0.3f;

    [MenuItem("Tools/Yantra/Build Loading Screen (จัดหน้าโหลด)")]
    private static void Build()
    {
        LoadingScreen screen = FindScreen();
        if (screen == null)
        {
            EditorUtility.DisplayDialog("Build Loading Screen",
                "หา LoadingScreen ไม่เจอ\nเปิด prefab UIRoot ค้างไว้ก่อน แล้วกดใหม่", "OK");
            return;
        }

        Sprite textSprite = FindSprite("Loading_Text");
        Sprite lineSprite = FindSprite("Loading_Line");
        Sprite shadeSprite = FindSprite("Loading_Shade");
        if (textSprite == null || lineSprite == null || shadeSprite == null)
        {
            EditorUtility.DisplayDialog("Build Loading Screen",
                "หารูป Loading_Text / Loading_Line / Loading_Shade ไม่เจอ\n" +
                "เช็คว่าใส่ไว้ในโปรเจกต์ และตั้ง Texture Type = Sprite (2D and UI) แล้ว", "OK");
            return;
        }

        Transform root = screen.transform;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Build Loading Screen");

        // ตำแหน่งใน PSD คิดที่ 1920x1080 — Canvas ตั้งค่าอื่นก็ย่อ/ขยายตาม
        Vector2 scale = Vector2.one;
        CanvasScaler scaler = root.GetComponentInParent<CanvasScaler>(true);
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            scale = new Vector2(scaler.referenceResolution.x / 1920f, scaler.referenceResolution.y / 1080f);

        // ---------- พื้นหลัง + เงา (อยู่ใต้ตัวหนังสือ tip ทั้งหมด) ----------
        Transform after = root.Find("Loading Black Screen");
        if (after == null) after = root.Find("VideoBG");
        int index = after != null ? after.GetSiblingIndex() + 1 : 0;

        Image bg = GetImage(root, "BG");
        Stretch(bg.rectTransform);
        bg.color = Color.white;
        bg.raycastTarget = false;
        bg.type = Image.Type.Simple;
        AspectRatioFitter fitter = bg.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = Undo.AddComponent<AspectRatioFitter>(bg.gameObject);
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 16f / 9f;
        bg.transform.SetSiblingIndex(index);
        // ยังไม่มีรูปในเกม — ปิดไว้ ไม่งั้นจอขาว (LoadingScreen เปิดให้เองตอนสุ่มได้รูป)
        if (bg.sprite == null) bg.gameObject.SetActive(false);

        Image shade = GetImage(root, "Shade");
        Stretch(shade.rectTransform);
        shade.sprite = shadeSprite;
        shade.color = Color.white;
        shade.raycastTarget = false;
        shade.type = Image.Type.Simple;
        shade.transform.SetSiblingIndex(bg.transform.GetSiblingIndex() + 1);
        shade.gameObject.SetActive(true);

        // ---------- ตัวหนังสือ + เส้นโหลด (บนสุด) ----------
        Image label = GetImage(root, "LoadingText");
        Place(label, textSprite, TextPos * scale, scale);
        label.color = Color.white;
        label.transform.SetAsLastSibling();

        Image lineBg = GetImage(root, "LineBG");
        Place(lineBg, lineSprite, LinePos * scale, scale);
        lineBg.color = new Color(1f, 1f, 1f, LineBgAlpha);
        lineBg.transform.SetAsLastSibling();

        Image lineFill = GetImage(root, "LineFill");
        Place(lineFill, lineSprite, LinePos * scale, scale);
        lineFill.color = Color.white;
        lineFill.type = Image.Type.Filled;
        lineFill.fillMethod = Image.FillMethod.Horizontal;
        lineFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        lineFill.fillAmount = 0f;
        lineFill.transform.SetAsLastSibling();

        // ---------- ต่อช่องใน LoadingScreen ----------
        var so = new SerializedObject(screen);
        SetRef(so, "_background", bg);
        SetRef(so, "_progressFill", lineFill);
        SetRef(so, "_loadingLabel", label);

        // ในแบบไม่มี spinner กับตัวเลข % — ปิดไว้ (ไม่ลบ)
        DisableRef(so, "_spinner");
        DisableRef(so, "_percentText");
        so.ApplyModifiedProperties();

        MarkDirty(root.gameObject);
        Selection.activeGameObject = root.gameObject;

        Debug.Log("[LoadingScreenBuilder] จัดหน้าโหลดเสร็จ — BG ยังปิดอยู่จนกว่าจะใส่รูปในช่อง Backgrounds", screen);
    }

    // ---------- Helpers ----------

    private static LoadingScreen FindScreen()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            LoadingScreen inPrefab = stage.prefabContentsRoot.GetComponentInChildren<LoadingScreen>(true);
            if (inPrefab != null) return inPrefab;
        }
        return Object.FindObjectsByType<LoadingScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .FirstOrDefault();
    }

    // รูปอาจตั้งเป็น Sprite Mode = Multiple (ชื่อจะเป็น xxx_0) — เอา sprite ตัวแรกในไฟล์
    private static Sprite FindSprite(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != fileName) continue;

            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite != null) return sprite;
        }
        return null;
    }

    private static Image GetImage(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        GameObject go;
        if (t == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Build Loading Screen");
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
        }
        else go = t.gameObject;

        Image image = go.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(go);
        return image;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    // ยึดมุมซ้ายล่าง ตามแบบ — จอกว้างกว่า 16:9 ก็ยังติดขอบซ้ายล่างเหมือนเดิม
    private static void Place(Image image, Sprite sprite, Vector2 pos, Vector2 scale)
    {
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(sprite.rect.width * scale.x, sprite.rect.height * scale.y);
        rt.anchoredPosition = pos;
        image.gameObject.SetActive(true);
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[LoadingScreenBuilder] ไม่เจอช่อง {field} — ใช้ LoadingScreen.cs ตัวใหม่หรือยัง?");
    }

    private static void DisableRef(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null || p.objectReferenceValue == null) return;

        GameObject go = p.objectReferenceValue as GameObject;
        if (go == null && p.objectReferenceValue is Component c) go = c.gameObject;
        if (go == null) return;

        Undo.RecordObject(go, "Build Loading Screen");
        go.SetActive(false);
    }

    private static void MarkDirty(GameObject go)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
        else EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
#endif
