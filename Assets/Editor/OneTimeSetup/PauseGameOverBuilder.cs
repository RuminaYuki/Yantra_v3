#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// จัดหน้า Pause กับ Game Over ตามไฟล์ pause2.psd / Game_over.psd ในคลิกเดียว
/// เปิด prefab UIRoot ค้างไว้ แล้วกด Tools > Yantra > Build Pause + Game Over
///
/// ทำอะไรบ้าง (Ctrl+Z ย้อนได้, กดซ้ำได้ ไม่สร้างซ้ำ):
/// - Shade : เงาดำไล่จากบนลงล่าง ทับพื้นหลังเดิม
/// - Frame : กรอบใหม่ (กว้างขึ้น) แทนกรอบเดิม / ปิดเส้นแนวตั้งเก่า (ไม่มีในแบบใหม่)
/// - Pause : เพิ่มปุ่ม Load Game (เปิดหน้าเลือกเซฟ) ใต้ Resume
/// - Game Over : เปลี่ยนรูปหัวข้อ + เพิ่มเลือดขอบจอ (Blood) ทับบนสุด
/// - ปุ่มทุกอัน: แถบแดงอยู่ "ใต้" ตัวหนังสือ, ตัวที่ไม่ได้เลือกจาง 50%, วางตำแหน่งตรง PSD
///
/// ต้องมีรูปพวกนี้ในโปรเจกต์ (ชื่อไฟล์ตรงตัว อยู่โฟลเดอร์ไหนก็ได้):
/// PauseV2_Bg, PauseV2_Frame, GameOverV2_Bg, GameOverV2_Frame, GameOverV2_Title, GameOverV2_Blood
/// </summary>
public static class PauseGameOverBuilder
{
    private const string UndoName = "Build Pause + Game Over";
    private const string Title = "Build Pause + Game Over";
    private const float PsdW = 1920f;
    private const float PsdH = 1080f;

    // รูปตัวหนังสือ Load Game ของหน้า Pause (มีอยู่แล้วในโปรเจกต์)
    private const string PauseLoadGamePath = "Assets/Ui/New Main/Pause/Loadgame.png";

    private static readonly string[] NewSprites =
    {
        "PauseV2_Bg", "PauseV2_Frame",
        "GameOverV2_Bg", "GameOverV2_Frame", "GameOverV2_Title", "GameOverV2_Blood",
    };

    // ตัวที่ไม่ได้เลือก = จาง 50% (ใน PSD ตั้ง Opacity 50%)
    private static readonly Color Dim = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

    private static Dictionary<string, Sprite> _sprites;
    private static readonly List<string> _report = new List<string>();

    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Build Pause + Game Over (จัดหน้า Pause - Game Over ตามแบบ)")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(Title, "ออกจาก Play Mode ก่อนนะ", "ตกลง");
            return;
        }

        _report.Clear();

        PauseScreen pause = FindScreen<PauseScreen>();
        GameOverScreen over = FindScreen<GameOverScreen>();
        if (pause == null && over == null)
        {
            EditorUtility.DisplayDialog(Title,
                "หา PauseScreen / GameOverScreen ไม่เจอ\n\nเปิด prefab UIRoot ค้างไว้ก่อน แล้วกดใหม่", "ตกลง");
            return;
        }

        if (!PrepareSprites()) return;

        if (pause != null) BuildPause(pause);
        else _report.Add("⚠ ไม่เจอ PauseScreen — ข้าม");

        if (over != null) BuildGameOver(over);
        else _report.Add("⚠ ไม่เจอ GameOverScreen — ข้าม");

        GameObject any = pause != null ? pause.gameObject : over.gameObject;
        MarkDirty(any);
        Selection.activeGameObject = any;

        string msg = string.Join("\n", _report);
        Debug.Log("[PauseGameOverBuilder]\n" + msg, any);
        EditorUtility.DisplayDialog(Title,
            msg + "\n\nเสร็จแล้ว — กด Ctrl+S เซฟ prefab\n(ไม่ชอบกด Ctrl+Z ย้อนได้)", "ตกลง");
    }

    // ================= ล้างค่าที่ฉากทับ prefab =================

    /// <summary>
    /// UIRoot ในฉากมักจำค่าตำแหน่ง/ขนาดปุ่มเก่าไว้ทับ prefab (สมัยที่ Layout Group จัดให้)
    /// ค่าพวกนี้ชนะ prefab — ปุ่มเลยกว้าง 0 มองไม่เห็น หรือไปโผล่ผิดที่
    /// เมนูนี้ล้างเฉพาะค่าที่ทับอยู่ใต้หน้า Pause กับ Game Over ในฉากที่เปิดอยู่ ส่วนอื่นของ UIRoot ไม่แตะ
    /// </summary>
    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Fix Pause + Game Over in Scene (ล้างค่าที่ฉากทับ prefab)")]
    private static void RevertSceneOverrides()
    {
        const string title = "Fix Pause + Game Over in Scene";

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(title, "ออกจาก Play Mode ก่อนนะ", "ตกลง");
            return;
        }
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(title,
                "ตอนนี้อยู่ใน Prefab Mode\n\nกดลูกศร < ตรงหัว Hierarchy กลับไปที่ฉากก่อน แล้วกดใหม่", "ตกลง");
            return;
        }

        var screens = new List<Transform>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PauseScreen s in root.GetComponentsInChildren<PauseScreen>(true)) screens.Add(s.transform);
                foreach (GameOverScreen s in root.GetComponentsInChildren<GameOverScreen>(true)) screens.Add(s.transform);
            }
        }

        if (screens.Count == 0)
        {
            EditorUtility.DisplayDialog(title, "ฉากที่เปิดอยู่ไม่มีหน้า Pause / Game Over", "ตกลง");
            return;
        }

        var reverted = new List<string>();
        var done = new HashSet<GameObject>();

        foreach (Transform screen in screens)
        {
            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(screen.gameObject);
            if (instanceRoot == null || !done.Add(instanceRoot)) continue;

            foreach (ObjectOverride o in PrefabUtility.GetObjectOverrides(instanceRoot, false))
            {
                Object obj = o.instanceObject;
                GameObject go = obj as GameObject;
                if (go == null && obj is Component c) go = c.gameObject;
                if (go == null) continue;

                bool underScreen = false;
                foreach (Transform s in screens)
                    if (go.transform.IsChildOf(s)) { underScreen = true; break; }
                if (!underScreen) continue;

                o.Revert(InteractionMode.UserAction);
                reverted.Add(go.name + " (" + obj.GetType().Name + ")");
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }

        string msg = reverted.Count == 0
            ? "ไม่มีค่าที่ฉากทับไว้แล้ว — ฉากนี้ใช้ค่าจาก prefab ตรง ๆ"
            : "ล้างค่าที่ฉากทับไว้ " + reverted.Count + " จุด:\n\n" + string.Join("\n", reverted.Take(25)) +
              (reverted.Count > 25 ? "\n..." : "") + "\n\nกด Ctrl+S เซฟฉาก";
        Debug.Log("[PauseGameOverBuilder] " + msg.Replace("\n\n", "\n"));
        EditorUtility.DisplayDialog(title, msg, "ตกลง");
    }

    // ================= Pause =================

    private static void BuildPause(PauseScreen screen)
    {
        Transform root = screen.transform;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, UndoName);

        // ---------- พื้นหลัง ----------
        Transform baseBg = root.Find("Background");
        int index = baseBg != null ? baseBg.GetSiblingIndex() + 1 : 0;

        Image shade = GetImage(root, "Shade");
        FullScreen(shade, _sprites["PauseV2_Bg"]);
        shade.transform.SetSiblingIndex(index);

        Image frame = GetOrRename(root, "Frame", "Background (4)");
        SetSprite(frame, _sprites["PauseV2_Frame"], keepAspect: false);
        PlacePsd(frame.rectTransform, root, 449f, 272f, 1022f, 539f);
        frame.transform.SetSiblingIndex(shade.transform.GetSiblingIndex() + 1);

        Hide(root.Find("Background (5)"), "เส้นแนวตั้งเก่า (Background (5))");

        // ---------- หัวข้อ Pause ----------
        Transform titleImage = root.Find("Title (1)/Image");
        if (titleImage != null) PlacePsd((RectTransform)titleImage, root, 781f, 312f, 332f, 113f);
        else _report.Add("⚠ ไม่เจอ Title (1)/Image — หัวข้อ Pause ไม่ได้ขยับ");

        // ---------- ปุ่ม ----------
        Dictionary<string, Button> buttons = ButtonsByMethod(root);
        buttons.TryGetValue("OnResumeClicked", out Button resume);
        buttons.TryGetValue("OnSettingsClicked", out Button settings);
        buttons.TryGetValue("OnMainMenuClicked", out Button mainMenu);
        buttons.TryGetValue("OnLoadGameClicked", out Button loadGame);

        if (resume == null || settings == null || mainMenu == null)
        {
            _report.Add("⚠ Pause: หาปุ่ม Resume / Settings / Main Menu ไม่ครบ (ดูจาก On Click) — ข้ามส่วนปุ่ม");
            return;
        }

        DisableLayout(resume.transform.parent);
        Sprite highlightSprite = FindHighlightSprite(resume);

        SetupButton(resume, "Btn_Resume", root, null, highlightSprite,
            text: new Rect(791f, 468f, 193f, 56f), highlight: new Vector2(780f, 464f), showHighlight: true);
        SetupButton(settings, "Btn_Settings", root, null, highlightSprite,
            text: new Rect(793f, 631f, 199f, 71f), highlight: new Vector2(782f, 626.5f), showHighlight: false);
        SetupButton(mainMenu, "Btn_MainMenu", root, null, highlightSprite,
            text: new Rect(793f, 708f, 283f, 53f), highlight: new Vector2(782f, 702.5f), showHighlight: false);

        // ---------- ปุ่ม Load Game (ใหม่) — ก๊อปจาก Main Menu ----------
        if (loadGame == null)
        {
            GameObject copy = Object.Instantiate(mainMenu.gameObject, mainMenu.transform.parent);
            Undo.RegisterCreatedObjectUndo(copy, UndoName);
            copy.name = "Btn_LoadGame";

            // ป้ายเสียงปุ่ม Back ไม่ควรติดมาด้วย
            foreach (UIBackButtonTag tag in copy.GetComponents<UIBackButtonTag>())
                Undo.DestroyObjectImmediate(tag);

            loadGame = copy.GetComponent<Button>();
            while (loadGame.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(loadGame.onClick, 0);
            UnityEventTools.AddPersistentListener(loadGame.onClick, screen.OnLoadGameClicked);
            _report.Add("Pause: เพิ่มปุ่ม Load Game (กดแล้วเปิดหน้าเลือกเซฟ)");
        }

        Sprite loadGameSprite = LoadSprite(PauseLoadGamePath);
        if (loadGameSprite == null) _report.Add("⚠ ไม่เจอรูป " + PauseLoadGamePath + " — ปุ่ม Load Game ใช้รูปเดิมไปก่อน");

        SetupButton(loadGame, "Btn_LoadGame", root, loadGameSprite, highlightSprite,
            text: new Rect(791f, 547f, 271f, 57f), highlight: new Vector2(780f, 543.5f), showHighlight: false);

        // เรียงใหม่: Resume > Load Game > Settings > Main Menu
        int first = resume.transform.GetSiblingIndex();
        loadGame.transform.SetSiblingIndex(first + 1);
        settings.transform.SetSiblingIndex(first + 2);
        mainMenu.transform.SetSiblingIndex(first + 3);

        // เปิดหน้ามาแล้วเลือก Resume ไว้ก่อน (แถบแดงโผล่ที่ Resume เหมือนในแบบ)
        var so = new SerializedObject(screen);
        SetRef(so, "_firstSelected", resume.gameObject);
        so.ApplyModifiedProperties();

        _report.Add("Pause: กรอบใหม่ + เงา + ปุ่ม 4 อันตรง PSD แล้ว");
    }

    // ================= Game Over =================

    private static void BuildGameOver(GameOverScreen screen)
    {
        Transform root = screen.transform;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, UndoName);

        // ---------- พื้นหลัง ----------
        Transform baseBg = root.Find("Background");
        int index = baseBg != null ? baseBg.GetSiblingIndex() + 1 : 0;

        Image shade = GetImage(root, "Shade");
        FullScreen(shade, _sprites["GameOverV2_Bg"]);
        shade.transform.SetSiblingIndex(index);

        Image frame = GetOrRename(root, "Frame", "Background (1)");
        SetSprite(frame, _sprites["GameOverV2_Frame"], keepAspect: false);
        PlacePsd(frame.rectTransform, root, 449f, 297f, 1022f, 451f);
        frame.transform.SetSiblingIndex(shade.transform.GetSiblingIndex() + 1);

        Hide(root.Find("Background (2)"), "เส้นแนวตั้งเก่า (Background (2))");

        // ---------- หัวข้อ Game Over ----------
        Image title = GetOrRename(root, "Title", "Image");
        SetSprite(title, _sprites["GameOverV2_Title"], keepAspect: true);
        PlacePsd(title.rectTransform, root, 647f, 344f, 649f, 146f);

        // ---------- ปุ่ม ----------
        Dictionary<string, Button> buttons = ButtonsByMethod(root);
        buttons.TryGetValue("OnRetryClicked", out Button retry);
        buttons.TryGetValue("OnMainMenuClicked", out Button mainMenu);

        if (retry != null && mainMenu != null)
        {
            DisableLayout(retry.transform.parent);
            Sprite highlightSprite = FindHighlightSprite(retry);

            SetupButton(retry, retry.name, root, null, highlightSprite,
                text: new Rect(661f, 519f, 271f, 57f), highlight: new Vector2(650f, 516f), showHighlight: true);
            SetupButton(mainMenu, mainMenu.name, root, null, highlightSprite,
                text: new Rect(663f, 600f, 283f, 53f), highlight: new Vector2(652f, 594.5f), showHighlight: false);
        }
        else _report.Add("⚠ Game Over: หาปุ่ม Load Game (Retry) / Main Menu ไม่ครบ — ข้ามส่วนปุ่ม");

        // ---------- เลือดขอบจอ (บนสุด) ----------
        Image blood = GetImage(root, "Blood");
        FullScreen(blood, _sprites["GameOverV2_Blood"]);
        blood.transform.SetAsLastSibling();

        _report.Add("Game Over: กรอบใหม่ + เงา + หัวข้อใหม่ + เลือดขอบจอ + ปุ่มตรง PSD แล้ว");
    }

    // ================= ปุ่ม =================

    /// <summary>
    /// ปุ่มหนึ่งอัน = ตัวปุ่ม (รับคลิก มองไม่เห็น) > Highlight (แถบแดง) > Label (รูปตัวหนังสือ บนสุด)
    /// เดิมรูปตัวหนังสืออยู่ที่ตัวปุ่ม แถบแดงเลยไปทับตัวหนังสือ — ย้ายรูปลงไปเป็นลูกชื่อ Label
    /// </summary>
    private static void SetupButton(Button button, string name, Transform screenRoot, Sprite textSprite,
        Sprite highlightSprite, Rect text, Vector2 highlight, bool showHighlight)
    {
        GameObject go = button.gameObject;
        go.name = name;
        go.SetActive(true);

        Image clickArea = go.GetComponent<Image>();
        Transform labelT = go.transform.Find("Label");
        Image label = labelT != null ? labelT.GetComponent<Image>() : null;

        if (label == null)
        {
            label = GetImage(go.transform, "Label");
            label.sprite = clickArea != null ? clickArea.sprite : null;
        }
        if (textSprite != null) label.sprite = textSprite;

        label.type = Image.Type.Simple;
        label.preserveAspect = true;
        label.raycastTarget = false;
        label.color = Color.white;
        Stretch(label.rectTransform);

        // ตัวปุ่มเหลือไว้รับคลิกอย่างเดียว (ใส ๆ)
        if (clickArea != null)
        {
            clickArea.sprite = null;
            clickArea.type = Image.Type.Simple;
            clickArea.color = Clear;
            clickArea.raycastTarget = true;
        }

        // สีปุ่ม: ปกติจาง 50% / ชี้ หรือ เลือก = ขาวเต็ม
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = label;
        ColorBlock colors = button.colors;
        colors.normalColor = Dim;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.25f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        // แถบแดง
        MenuButtonEffect effect = go.GetComponent<MenuButtonEffect>();
        SerializedObject so = effect != null ? new SerializedObject(effect) : null;

        Image hl = null;
        if (so != null) hl = so.FindProperty("_highlightImage")?.objectReferenceValue as Image;
        if (hl == null)
        {
            Transform old = go.transform.Find("Highlight");
            if (old == null) old = go.transform.Find("Image");
            if (old != null) hl = old.GetComponent<Image>();
        }
        if (hl == null) hl = GetImage(go.transform, "Highlight");

        hl.gameObject.name = "Highlight";
        hl.gameObject.SetActive(true);
        if (hl.sprite == null) hl.sprite = highlightSprite;
        hl.type = Image.Type.Filled;
        hl.fillMethod = Image.FillMethod.Horizontal;
        hl.fillOrigin = (int)Image.OriginHorizontal.Left;
        hl.fillAmount = 1f;
        hl.preserveAspect = false;
        hl.raycastTarget = false;
        // ตอนเล่น MenuButtonEffect คุมความเข้มเอง — ตรงนี้แค่ให้ดูใน prefab ได้เหมือนแบบ
        hl.color = showHighlight ? Color.white : Clear;

        // วางตำแหน่ง — ตัวปุ่มก่อน แล้วค่อยแถบแดง (แถบแดงเป็นลูกของปุ่ม)
        PlacePsd((RectTransform)go.transform, screenRoot, text.x, text.y, text.width, text.height);
        PlacePsd(hl.rectTransform, screenRoot, highlight.x, highlight.y, 216f, 65f);

        // ลำดับวาด: แถบแดงล่างสุด ตัวหนังสือบนสุด
        hl.transform.SetSiblingIndex(0);
        label.transform.SetAsLastSibling();

        if (so != null)
        {
            SetRef(so, "_highlightImage", hl);
            SetFloat(so, "_highlightAlpha", 1f);
            SetBool(so, "_highlightWipe", true);
            so.ApplyModifiedProperties();
        }
    }

    private static Dictionary<string, Button> ButtonsByMethod(Transform root)
    {
        var map = new Dictionary<string, Button>();
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
        {
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            {
                string method = b.onClick.GetPersistentMethodName(i);
                if (!string.IsNullOrEmpty(method) && !map.ContainsKey(method)) map[method] = b;
            }
        }
        return map;
    }

    private static Sprite FindHighlightSprite(Button button)
    {
        MenuButtonEffect effect = button.GetComponent<MenuButtonEffect>();
        if (effect != null)
        {
            var so = new SerializedObject(effect);
            var img = so.FindProperty("_highlightImage")?.objectReferenceValue as Image;
            if (img != null && img.sprite != null) return img.sprite;
        }
        foreach (Image img in button.GetComponentsInChildren<Image>(true))
            if (img.gameObject != button.gameObject && img.sprite != null && img.name != "Label") return img.sprite;
        return null;
    }

    private static void DisableLayout(Transform group)
    {
        if (group == null) return;
        // ปุ่มวางตามตำแหน่งใน PSD เอง (ระยะห่างแต่ละปุ่มไม่เท่ากัน) — ปิด Layout Group ไว้ ไม่ลบ
        foreach (LayoutGroup layout in group.GetComponents<LayoutGroup>()) layout.enabled = false;
        foreach (ContentSizeFitter fitter in group.GetComponents<ContentSizeFitter>()) fitter.enabled = false;
    }

    // ================= รูป =================

    private static bool PrepareSprites()
    {
        _sprites = new Dictionary<string, Sprite>();
        var missing = new List<string>();

        foreach (string name in NewSprites)
        {
            string path = FindTexturePath(name);
            if (path == null)
            {
                missing.Add(name + ".png");
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                // รูปเต็มจอบีบแบบคุณภาพสูง (กินแรมน้อยลง 4 เท่า) / รูปเล็กไม่บีบ ให้คมเหมือน PSD
                TextureImporterCompression wanted = width >= 1920
                    ? TextureImporterCompression.CompressedHQ
                    : TextureImporterCompression.Uncompressed;

                if (importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single ||
                    importer.textureCompression != wanted ||
                    importer.mipmapEnabled ||
                    importer.maxTextureSize < 2048)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.textureCompression = wanted;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.maxTextureSize = 2048;
                    importer.SaveAndReimport();
                    _report.Add("ตั้งรูป " + name + " เป็น Sprite");
                }
            }

            Sprite sprite = LoadSprite(path);
            if (sprite == null) missing.Add(name + ".png (ยังไม่เป็น Sprite)");
            else _sprites[name] = sprite;
        }

        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog(Title,
                "ยังไม่มีรูปพวกนี้ในโปรเจกต์:\n\n" + string.Join("\n", missing) +
                "\n\nเช็คโฟลเดอร์ Assets/Ui/New Main/PSD_v2 ก่อน", "ตกลง");
            return false;
        }
        return true;
    }

    private static string FindTexturePath(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return path;
        }
        return null;
    }

    // รูปเก่าบางรูปตั้ง Sprite Mode = Multiple — เอา sprite ตัวแรกในไฟล์
    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    // ================= Helpers =================

    private static T FindScreen<T>() where T : Component
    {
        var found = new List<T>();

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
            found.AddRange(stage.prefabContentsRoot.GetComponentsInChildren<T>(true));

        if (found.Count == 0)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    found.AddRange(root.GetComponentsInChildren<T>(true));
            }
        }

        foreach (T s in found)
            if (s.gameObject.activeSelf) return s;
        return found.Count > 0 ? found[0] : null;
    }

    private static Image GetImage(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        GameObject go;
        if (t == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
        }
        else go = t.gameObject;

        Image image = go.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(go);
        return image;
    }

    // ใช้ตัวที่ชื่อใหม่ถ้ามีแล้ว ไม่งั้นเอาตัวเก่ามาเปลี่ยนชื่อ ไม่มีทั้งคู่ก็สร้างใหม่
    private static Image GetOrRename(Transform parent, string newName, string oldName)
    {
        if (parent.Find(newName) == null)
        {
            Transform old = parent.Find(oldName);
            if (old != null) old.gameObject.name = newName;
        }
        return GetImage(parent, newName);
    }

    private static void SetSprite(Image image, Sprite sprite, bool keepAspect)
    {
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = keepAspect;
        image.raycastTarget = false;
        image.color = Color.white;
        image.gameObject.SetActive(true);
    }

    private static void FullScreen(Image image, Sprite sprite)
    {
        SetSprite(image, sprite, keepAspect: false);
        Stretch(image.rectTransform);
    }

    private static void Hide(Transform t, string label)
    {
        if (t == null || !t.gameObject.activeSelf) return;
        t.gameObject.SetActive(false);
        _report.Add("ปิด " + label + " ไว้ (ไม่ได้ลบ)");
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

    /// <summary>
    /// วางตามกรอบใน PSD 1920x1080 (x, y = มุมซ้ายบนแบบ Photoshop) — ยึดกลางจอ
    /// อยู่ลึกกี่ชั้นก็ได้ ขอแค่พ่อแต่ละชั้นยึดกลาง หรือยืดเต็ม
    /// </summary>
    private static void PlacePsd(RectTransform rt, Transform screenRoot, float x, float y, float w, float h)
    {
        var target = new Vector2(x + w * 0.5f - PsdW * 0.5f, PsdH * 0.5f - (y + h * 0.5f));

        for (Transform p = rt.parent; p != null && p != screenRoot; p = p.parent)
        {
            var pr = p as RectTransform;
            if (pr == null) break;

            Vector2 mid = (pr.anchorMin + pr.anchorMax) * 0.5f;
            if (Mathf.Abs(mid.x - 0.5f) > 0.001f || Mathf.Abs(mid.y - 0.5f) > 0.001f)
                _report.Add("⚠ " + pr.name + " ไม่ได้ยึดกลาง — " + rt.name + " อาจเพี้ยน");

            target -= pr.anchoredPosition + Vector2.Scale(pr.sizeDelta, new Vector2(0.5f, 0.5f) - pr.pivot);
        }

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = target;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else _report.Add("⚠ ไม่เจอช่อง " + field + " ใน " + so.targetObject.GetType().Name);
    }

    private static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.floatValue = value;
    }

    private static void SetBool(SerializedObject so, string field, bool value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.boolValue = value;
    }

    private static void MarkDirty(GameObject go)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
        else EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
#endif
