#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// จัดหน้า Load ให้ตรงกับ PSD ในคลิกเดียว — เมนู Tools > Yantra > Build Load Page
///
/// ทำอะไรบ้าง
/// 1. ตั้งรูปทั้ง 8 รูปเป็น Sprite / Compression None ให้เอง (แค่ลาก PNG เข้าโปรเจกต์ที่ไหนก็ได้)
/// 2. แก้ prefab SaveSlot: SlotBG, Glow, Selected, PicArea (ImageBG + PicFade), ตัวหนังสือ 4 อัน, Line
/// 3. แก้หน้า LoadGameScreen: Shade, TitleImage, ScrollLine, ScrollDot, วาง 3 ช่อง, Back, Load/Delete
/// 4. ผูกช่องในสคริปต์ Load Save Screen ให้ครบ
///
/// กดซ้ำได้ — ของที่มีแล้วจะถูกตั้งค่าใหม่ ไม่สร้างซ้ำ
/// ฝั่งหน้าจอกด Ctrl+Z ย้อนได้ ส่วน prefab SaveSlot ย้อนด้วย Unity VCS / git
/// </summary>
public static class LoadPageBuilder
{
    private const string UndoName = "Build Load Page";

    private static readonly string[] SpriteNames =
    {
        "SlotBG", "SlotGlow", "PicFade", "SlotLine", "ScrollLine", "ScrollDot", "LoadTitle", "LoadShade",
    };

    private static readonly Color White = Color.white;
    private static readonly Color SmallText = new Color(1f, 1f, 1f, 179f / 255f);

    private static Dictionary<string, Sprite> _sprites;
    private static TMP_FontAsset _font;
    private static readonly List<string> _report = new List<string>();

    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Build Load Page (จัดหน้า Load ตามแบบ)")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Build Load Page", "ออกจาก Play Mode ก่อนนะ", "ตกลง");
            return;
        }

        _report.Clear();

        LoadSaveScreen screen = FindScreen();
        if (screen == null)
        {
            EditorUtility.DisplayDialog("Build Load Page",
                "หา Load Save Screen ไม่เจอ\n\nเปิด prefab UIRoot (หรือ scene ที่มีหน้า Load) แล้วกดใหม่", "ตกลง");
            return;
        }

        if (!PrepareSprites()) return;
        _font = FindFont();

        // ขั้นแรก: แก้ตัว prefab SaveSlot แล้วเซฟ
        // ขั้นสอง (เฟรมถัดไป): แก้หน้าจอ — รอให้ Unity อัปเดตช่องในหน้าจอตาม prefab ใหม่ก่อน
        BuildSlotPrefab(screen);

        EditorApplication.delayCall += () => BuildScreenLater(screen);
    }

    // ---------- หาหน้าจอ ----------

    private static LoadSaveScreen FindScreen()
    {
        if (Selection.activeGameObject != null)
        {
            var fromSelection = Selection.activeGameObject.GetComponentInParent<LoadSaveScreen>(true);
            if (fromSelection != null) return fromSelection;
        }

        var found = new List<LoadSaveScreen>();

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
            found.AddRange(stage.prefabContentsRoot.GetComponentsInChildren<LoadSaveScreen>(true));

        if (found.Count == 0)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    found.AddRange(root.GetComponentsInChildren<LoadSaveScreen>(true));
            }
        }

        // มีหลายตัว (เช่น LoadGameScreen เก่าที่ปิดไว้) เลือกตัวที่เปิดอยู่
        foreach (var s in found)
            if (s.gameObject.activeSelf) return s;

        return found.Count > 0 ? found[0] : null;
    }

    // ---------- รูป ----------

    private static bool PrepareSprites()
    {
        _sprites = new Dictionary<string, Sprite>();
        var missing = new List<string>();

        foreach (string name in SpriteNames)
        {
            string path = FindTexturePath(name);
            if (path == null)
            {
                missing.Add(name + ".png");
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode != SpriteImportMode.Single ||
                 importer.textureCompression != TextureImporterCompression.Uncompressed ||
                 importer.mipmapEnabled))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                _report.Add($"ตั้งรูป {name} เป็น Sprite / Compression None");
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) missing.Add(name + ".png (ยังไม่เป็น Sprite)");
            else _sprites[name] = sprite;
        }

        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("Build Load Page",
                "ยังไม่มีรูปพวกนี้ในโปรเจกต์:\n\n" + string.Join("\n", missing) +
                "\n\nลาก PNG จาก LoadPage_Sprites.zip กับ LoadShade.png เข้าโปรเจกต์ก่อน", "ตกลง");
            return false;
        }
        return true;
    }

    private static string FindTexturePath(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return path;
        }
        return null;
    }

    private static TMP_FontAsset FindFont()
    {
        TMP_FontAsset fallback = null;
        foreach (string guid in AssetDatabase.FindAssets("RSU t:TMP_FontAsset"))
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (font == null) continue;
            if (font.name == "RSU Text_Regular SDF") return font;
            if (fallback == null && font.name.Contains("RSU Text_Regular")) fallback = font;
        }

        if (fallback == null) _report.Add("⚠ ไม่เจอฟอนต์ RSU Text_Regular SDF — ใช้ฟอนต์เดิมของแต่ละตัวหนังสือ");
        return fallback;
    }

    // ---------- prefab SaveSlot ----------

    private static void BuildSlotPrefab(LoadSaveScreen screen)
    {
        var slots = screen.GetComponentsInChildren<SaveSlotUI>(true);
        if (slots.Length == 0)
        {
            _report.Add("⚠ ไม่เจอ SaveSlot ใต้หน้าจอ");
            return;
        }

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(slots[0].gameObject);

        // ไม่ใช่ prefab — แก้ทีละช่องตรง ๆ
        if (string.IsNullOrEmpty(path))
        {
            foreach (var slot in slots) BuildSlot(slot.gameObject, undo: true);
            _report.Add("แก้ข้างในช่องเซฟทั้ง " + slots.Length + " ช่อง (ไม่ได้เป็น prefab)");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            BuildSlot(contents, undo: false);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            _report.Add("แก้ prefab " + path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void BuildSlot(GameObject root, bool undo)
    {
        var ui = root.GetComponent<SaveSlotUI>();
        if (ui == null) return;

        // อ่านช่องของ SaveSlotUI ผ่าน SerializedObject — ไม่ต้องแก้ไฟล์ของเพื่อน
        var so = new SerializedObject(ui);
        var imageBG = so.FindProperty("imageBG").objectReferenceValue as Image;
        var slotNumber = so.FindProperty("slotNumber").objectReferenceValue as TMP_Text;
        var saveTime = so.FindProperty("textSaveTime").objectReferenceValue as TMP_Text;
        var playTime = so.FindProperty("textPlayTime").objectReferenceValue as TMP_Text;
        var location = so.FindProperty("textLocation").objectReferenceValue as TMP_Text;
        var button = so.FindProperty("button").objectReferenceValue as Button;
        if (button == null) button = root.GetComponent<Button>();

        Transform t = root.transform;
        var rootRect = (RectTransform)t;
        Rec(rootRect, undo);
        Place(rootRect, new Vector2(0f, 1f), new Vector2(0f, 1f), rootRect.anchoredPosition, new Vector2(978f, 151f));

        // พื้นช่อง
        var bg = GetOrAdd<Image>(root, undo);
        Rec(bg, undo);
        bg.sprite = _sprites["SlotBG"];
        bg.type = Image.Type.Simple;
        bg.color = White;
        bg.raycastTarget = true;   // ตัวรับคลิกของช่อง

        // แสงตอนชี้ — ให้ Button ย้อมสีตัวนี้แทนพื้นช่อง (พื้นช่องเป็นสีดำ ย้อมยังไงก็ไม่เห็น)
        Image glow = ChildImage(t, "Glow", "SlotGlow", undo);
        Stretch(glow.rectTransform);
        glow.color = White;
        glow.transform.SetSiblingIndex(0);

        if (button != null)
        {
            Rec(button, undo);
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = glow;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 30f / 255f);
            colors.pressedColor = new Color(1f, 1f, 1f, 50f / 255f);
            colors.selectedColor = new Color(1f, 1f, 1f, 30f / 255f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
        }

        // แสงค้างตอนเลือกช่อง
        Image selected = ChildImage(t, "Selected", "SlotGlow", undo);
        Stretch(selected.rectTransform);
        selected.color = new Color(1f, 1f, 1f, 60f / 255f);
        selected.transform.SetSiblingIndex(1);
        GetOrAdd<SaveSlotSelectedMark>(selected.gameObject, undo);

        // กรอบรูปฉาก — ตัดส่วนเกินทิ้ง รูปเต็มกรอบไม่บี้ แล้วทับด้วยรอยจางเป็นเม็ด
        RectTransform picArea = Child(t, "PicArea", undo);
        Rec(picArea, undo);
        Place(picArea, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(409f, 151f));
        picArea.SetSiblingIndex(2);
        GetOrAdd<RectMask2D>(picArea.gameObject, undo);

        if (imageBG != null)
        {
            if (imageBG.transform.parent != picArea)
            {
                if (undo) Undo.SetTransformParent(imageBG.transform, picArea, UndoName);
                else imageBG.transform.SetParent(picArea, false);
            }

            Rec(imageBG, undo);
            imageBG.raycastTarget = false;
            imageBG.preserveAspect = false;
            imageBG.color = White;
            imageBG.transform.SetSiblingIndex(0);

            var fitter = GetOrAdd<AspectRatioFitter>(imageBG.gameObject, undo);
            Rec(fitter, undo);
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
        }
        else
        {
            _report.Add("⚠ SaveSlotUI ไม่ได้ใส่ช่อง Image BG");
        }

        Image picFade = ChildImage(picArea, "PicFade", "PicFade", undo);
        Stretch(picFade.rectTransform);
        picFade.color = White;
        picFade.transform.SetAsLastSibling();

        // ตัวหนังสือ
        SetupText(location, 0f, 376f, -41f, 520f, 50f, 36f, TextAlignmentOptions.Left, White, undo, ellipsis: true);
        SetupText(slotNumber, 0f, 376f, -93f, 300f, 26f, 22f, TextAlignmentOptions.Left, SmallText, undo);
        SetupText(saveTime, 0f, 376f, -116f, 380f, 26f, 22f, TextAlignmentOptions.Left, SmallText, undo);
        SetupText(playTime, 1f, 933f, -116f, 380f, 26f, 22f, TextAlignmentOptions.Right, SmallText, undo);

        // เส้นใต้ชื่อสถานที่
        Image line = ChildImage(t, "Line", "SlotLine", undo);
        Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(373f, -69f), new Vector2(547f, 1f));
        line.color = White;
        if (location != null && location.transform.parent == t)
            line.transform.SetSiblingIndex(location.transform.GetSiblingIndex() + 1);
    }

    private static void SetupText(TMP_Text text, float pivotX, float x, float y, float w, float h,
                                  float size, TextAlignmentOptions align, Color color, bool undo, bool ellipsis = false)
    {
        if (text == null) return;

        Rec(text.rectTransform, undo);
        Rec(text, undo);

        Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(pivotX, 0.5f), new Vector2(x, y), new Vector2(w, h));
        if (_font != null) text.font = _font;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = ellipsis ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    // ---------- หน้าจอ ----------

    private static void BuildScreenLater(LoadSaveScreen screen)
    {
        // ถ้า Unity โหลดหน้าจอใหม่หลังเซฟ prefab ตัวแปรเดิมจะหายไป — หาใหม่
        if (screen == null) screen = FindScreen();
        if (screen == null)
        {
            Debug.LogError("[Build Load Page] หาหน้าจอไม่เจอหลังเซฟ prefab — กดเมนูอีกรอบ");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        int group = Undo.GetCurrentGroup();

        BuildScreen(screen);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);

        Selection.activeGameObject = screen.gameObject;
        EditorGUIUtility.PingObject(screen.gameObject);

        string message = "เสร็จแล้ว\n\n• " + string.Join("\n• ", _report);
        Debug.Log("[Build Load Page] " + message.Replace("\n", " "), screen);
        EditorUtility.DisplayDialog("Build Load Page", message, "ตกลง");
    }

    private static void BuildScreen(LoadSaveScreen screen)
    {
        Transform root = screen.transform;
        const bool undo = true;

        CheckCanvasScaler(screen);

        // ของเหลือจากหน้า Scene Select — ปิดไว้ (ลบเองทีหลังได้)
        Deactivate(root, "ButtonContainer");
        Deactivate(root, "CountText");
        Deactivate(root, "Title");

        // พื้นหลัง: ไม่มีรูป = สีเทาเหมือน PSD / มีรูป = สีจริงของรูป
        Image background = root.Find("Background") != null ? root.Find("Background").GetComponent<Image>() : null;
        if (background != null)
        {
            Rec(background, undo);
            background.color = background.sprite == null ? new Color(60f / 255f, 60f / 255f, 60f / 255f, 1f) : White;
            background.raycastTarget = true;   // บังคลิกไม่ให้ทะลุไปหน้าข้างล่าง
            if (background.sprite == null) _report.Add("Background ยังไม่มีรูป — ใส่รูปศาลเจ้าใน Source Image แล้วตั้ง Color เป็น FFFFFF");
        }
        else
        {
            _report.Add("⚠ ไม่เจอลูกชื่อ Background");
        }

        Image shade = ChildImage(root, "Shade", "LoadShade", undo);
        Stretch(shade.rectTransform);
        shade.color = White;
        shade.transform.SetSiblingIndex(background != null ? background.transform.GetSiblingIndex() + 1 : 0);

        Image title = ChildImage(root, "TitleImage", "LoadTitle", undo);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -216f), new Vector2(293f, 52f));
        title.color = White;

        Image scrollLine = ChildImage(root, "ScrollLine", "ScrollLine", undo);
        Place(scrollLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(149f, -307f), new Vector2(3f, 480f));
        scrollLine.color = White;

        Image scrollDot = ChildImage(root, "ScrollDot", "ScrollDot", undo);
        Place(scrollDot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(143f, -303f), new Vector2(16f, 15f));
        scrollDot.color = White;

        // 3 ช่อง
        var slots = screen.GetComponentsInChildren<SaveSlotUI>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            RevertSlotOverrides(slots[i]);

            var rect = (RectTransform)slots[i].transform;
            Rec(rect, undo);
            Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -301f - 169f * i), new Vector2(978f, 151f));
        }
        _report.Add("วางช่องเซฟ " + slots.Length + " ช่อง");

        // Back
        Button back = FindBackButton(root);
        Button load = null, delete = null;
        if (back != null)
        {
            SetupButton(back, "Back", new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(267f, 167f),
                        new Vector2(130f, 60f), 44f, TextAlignmentOptions.Left, slide: -1f);

            Rec(back, undo);
            while (back.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(back.onClick, 0);
            UnityEventTools.AddPersistentListener(back.onClick, screen.OnBackClicked);

            load = BottomButton(root, back, "LoadButton", "Load", -454f, 200f);
            delete = BottomButton(root, back, "DeleteButton", "Delete", -305f, 140f);
        }
        else
        {
            _report.Add("⚠ ไม่เจอปุ่ม Back (หาจากชื่อ BackButton หรือ UI Back Button Tag) — เลยไม่ได้สร้าง Load/Delete");
        }

        // หน้า Load ในเมนูหลักไม่มีอะไรให้เซฟ — เอาปุ่ม Save ออก
        Transform oldSave = root.Find("SaveButton");
        if (oldSave != null)
        {
            Undo.DestroyObjectImmediate(oldSave.gameObject);
            _report.Add("ลบปุ่ม Save");
        }

        // ผูกช่องในสคริปต์
        var so = new SerializedObject(screen);
        SetRef(so, "loadButton", load);
        SetRef(so, "deleteButton", delete);
        SetRef(so, "marker", scrollDot.rectTransform);
        SetRef(so, "background", background);
        so.ApplyModifiedProperties();
        _report.Add("ผูก Load / Delete / Marker / Background ในสคริปต์ Load Save Screen แล้ว");

        // รูปเทียบ (ถ้ามี) ต้องอยู่บนสุดเสมอ
        Transform reference = root.Find("Ref");
        if (reference != null) reference.SetAsLastSibling();
    }

    private static Button FindBackButton(Transform root)
    {
        Transform named = root.Find("BackButton");
        if (named != null && named.GetComponent<Button>() != null) return named.GetComponent<Button>();

        foreach (Transform child in root)
        {
            if (child.GetComponent<UIBackButtonTag>() != null && child.GetComponent<Button>() != null)
                return child.GetComponent<Button>();
        }
        return null;
    }

    private static Button BottomButton(Transform root, Button template, string name, string label, float x, float width)
    {
        Transform existing = root.Find(name);
        GameObject go;

        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = Object.Instantiate(template.gameObject, root);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, UndoName);
        }

        // ก๊อปมาจาก Back — เอาป้ายเสียง Back กับคำสั่งย้อนกลับออก
        var tag = go.GetComponent<UIBackButtonTag>();
        if (tag != null) Undo.DestroyObjectImmediate(tag);

        var button = go.GetComponent<Button>();
        Rec(button, true);
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);

        SetupButton(button, label, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x, 163f),
                    new Vector2(width, 50f), 34f, TextAlignmentOptions.Center, slide: 0f);
        return button;
    }

    // slide < 0 = ไม่แตะค่า Slide Distance
    private static void SetupButton(Button button, string label, Vector2 anchor, Vector2 pivot, Vector2 pos,
                                    Vector2 size, float fontSize, TextAlignmentOptions align, float slide)
    {
        var rect = (RectTransform)button.transform;
        Rec(rect, true);
        Place(rect, anchor, pivot, pos, size);

        // ปุ่มล่องหน แต่ยังคลิกได้
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            Rec(image, true);
            Color c = image.color;
            c.a = 0f;
            image.color = c;
            image.raycastTarget = true;
        }

        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            Rec(text.rectTransform, true);
            Rec(text, true);
            Stretch(text.rectTransform);
            text.text = label;
            if (_font != null) text.font = _font;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }

        var effect = button.GetComponent<MenuButtonEffect>();
        if (effect != null)
        {
            var so = new SerializedObject(effect);
            SetColor(so, "_normalColor", White);
            SetColor(so, "_hoverColor", White);
            SetColor(so, "_disabledColor", new Color(1f, 1f, 1f, 153f / 255f));
            if (slide >= 0f)
            {
                var p = so.FindProperty("_slideDistance");
                if (p != null) p.floatValue = slide;
            }
            so.ApplyModifiedProperties();
        }
    }

    // ช่องที่ถูกแก้ทีละช่องมาก่อน (override) จะบังของใหม่จาก prefab — คืนค่าเฉพาะส่วนที่สคริปต์นี้ดูแล
    private static void RevertSlotOverrides(SaveSlotUI slot)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(slot)) return;

        var targets = new List<Object>();
        var so = new SerializedObject(slot);
        foreach (string field in new[] { "imageBG", "slotNumber", "textSaveTime", "textPlayTime", "textLocation" })
        {
            var component = so.FindProperty(field).objectReferenceValue as Component;
            if (component == null) continue;
            targets.Add(component);
            targets.Add(component.transform);
        }
        targets.Add(slot.GetComponent<Image>());
        targets.Add(slot.GetComponent<Button>());

        foreach (Object target in targets)
        {
            if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RevertObjectOverride(target, InteractionMode.UserAction);
        }
    }

    private static void CheckCanvasScaler(LoadSaveScreen screen)
    {
        var scaler = screen.GetComponentInParent<CanvasScaler>(true);
        if (scaler == null) return;

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
            scaler.referenceResolution != new Vector2(1920f, 1080f))
        {
            _report.Add($"⚠ Canvas Scaler ไม่ใช่ Scale With Screen Size 1920×1080 (ตอนนี้ {scaler.uiScaleMode} {scaler.referenceResolution}) — ตำแหน่งอาจเพี้ยน");
        }
    }

    // ---------- ตัวช่วย ----------

    private static void Deactivate(Transform root, string name)
    {
        Transform t = root.Find(name);
        if (t == null || !t.gameObject.activeSelf) return;
        Undo.RecordObject(t.gameObject, UndoName);
        t.gameObject.SetActive(false);
        _report.Add("ปิด " + name);
    }

    private static RectTransform Child(Transform parent, string name, bool undo)
    {
        Transform found = parent.Find(name);
        if (found != null) return (RectTransform)found;

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        if (undo) Undo.RegisterCreatedObjectUndo(go, UndoName);
        return (RectTransform)go.transform;
    }

    private static Image ChildImage(Transform parent, string name, string spriteName, bool undo)
    {
        RectTransform rect = Child(parent, name, undo);
        Image image = GetOrAdd<Image>(rect.gameObject, undo);
        Rec(rect, undo);
        Rec(image, undo);
        image.sprite = _sprites[spriteName];
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        rect.localScale = Vector3.one;
        return image;
    }

    private static T GetOrAdd<T>(GameObject go, bool undo) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component != null) return component;
        return undo ? Undo.AddComponent<T>(go) : go.AddComponent<T>();
    }

    private static void Rec(Object target, bool undo)
    {
        if (undo && target != null) Undo.RecordObject(target, UndoName);
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property == null)
        {
            _report.Add($"⚠ สคริปต์ Load Save Screen ไม่มีช่อง {field} — วาง LoadSaveScreen.cs ตัวล่าสุดหรือยัง");
            return;
        }
        if (value != null) property.objectReferenceValue = value;
    }

    private static void SetColor(SerializedObject so, string field, Color color)
    {
        var property = so.FindProperty(field);
        if (property != null) property.colorValue = color;
    }
}
#endif