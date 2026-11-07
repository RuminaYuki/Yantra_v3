using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// สร้างไอคอนกระเป๋าจากโมเดลของ ItemData — ขนาดรูปตามจำนวนช่อง, ซูมพอดีช่อง, แสงล็อกไม่โดนฉากกวน
public class ItemIconGenerator : EditorWindow
{
    private const string DefaultProfilePath = "Assets/Editor/ItemIconProfile.asset";
    private const int PreviewPixelsPerCell = 128;

    private static readonly Color LiningColor = new Color(0.2f, 0.21f, 0.13f);
    private static readonly Color StitchColor = new Color(0.86f, 0.8f, 0.64f, 0.55f);
    private static readonly GUIContent[] PpcLabels = { new GUIContent("120"), new GUIContent("180"), new GUIContent("240"), new GUIContent("360") };
    private static readonly int[] PpcValues = { 120, 180, 240, 360 };

    [SerializeField] private ItemIconProfile profile;
    [SerializeField] private ItemData current;
    [SerializeField] private List<ItemData> ticked = new List<ItemData>();

    private readonly List<ItemData> allItems = new List<ItemData>();
    private string search = "";
    private Vector2 listScroll;
    private Vector2 rightScroll;
    private bool showGlobal;
    private SerializedObject serializedProfile;
    private GUIStyle statusStyle;

    private IconStage stage;
    private Texture2D preview;
    private ItemData previewItem;
    private string previewNote = "";
    private bool previewQueued;

    [MenuItem("Tools/Item Icon Generator (HDRP)")]
    private static void Open() => GetWindow<ItemIconGenerator>("Item Icons");

    private void OnEnable()
    {
        minSize = new Vector2(760f, 480f);
        LoadProfile();
        RefreshItems();
        Undo.undoRedoPerformed += QueuePreview;
        QueuePreview();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= QueuePreview;
        stage?.Dispose();
        stage = null;
        if (preview != null) DestroyImmediate(preview);
    }

    private void OnFocus()
    {
        RefreshItems();
        Repaint();
    }

    // ---------- Data ----------

    private void LoadProfile()
    {
        if (profile != null) return;

        string[] guids = AssetDatabase.FindAssets("t:ItemIconProfile");
        if (guids.Length > 0)
        {
            profile = AssetDatabase.LoadAssetAtPath<ItemIconProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Editor")) AssetDatabase.CreateFolder("Assets", "Editor");
        profile = CreateInstance<ItemIconProfile>();
        AssetDatabase.CreateAsset(profile, DefaultProfilePath);
        AssetDatabase.SaveAssets();
    }

    private void RefreshItems()
    {
        allItems.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) allItems.Add(item);
        }
        allItems.Sort((a, b) => string.Compare(Label(a), Label(b), StringComparison.OrdinalIgnoreCase));
        ticked.RemoveAll(i => i == null);
    }

    private List<ItemData> Filtered()
    {
        if (string.IsNullOrWhiteSpace(search)) return allItems;

        var list = new List<ItemData>();
        foreach (ItemData item in allItems)
        {
            if (Label(item).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                list.Add(item);
        }
        return list;
    }

    private static string Label(ItemData item) =>
        string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;

    private ItemIconProfile.ItemSettings SettingsFor(ItemData item) =>
        profile.Find(item) ?? new ItemIconProfile.ItemSettings { item = item, rotation = item.previewRotation };

    private bool IsToolIcon(Sprite sprite)
    {
        if (sprite == null || profile == null) return false;
        string folder = profile.outputFolder.TrimEnd('/') + "/";
        return AssetDatabase.GetAssetPath(sprite).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsHandmade(ItemData item) => item.icon != null && !IsToolIcon(item.icon);

    // ---------- GUI ----------

    private void OnGUI()
    {
        if (profile == null) LoadProfile();
        if (statusStyle == null) statusStyle = new GUIStyle(EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        DrawList();
        DrawRight();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(300f));

        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
        List<ItemData> items = Filtered();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("ติ๊กทั้งหมด", EditorStyles.miniButtonLeft))
        {
            ticked.Clear();
            ticked.AddRange(items);
        }
        if (GUILayout.Button("ยังไม่มีไอคอน", EditorStyles.miniButtonMid))
        {
            ticked.Clear();
            foreach (ItemData item in items)
                if (item.icon == null && item.PreviewSource != null) ticked.Add(item);
        }
        if (GUILayout.Button("ไม่ติ๊ก", EditorStyles.miniButtonRight)) ticked.Clear();
        EditorGUILayout.EndHorizontal();

        listScroll = EditorGUILayout.BeginScrollView(listScroll);
        foreach (ItemData item in items) DrawRow(item);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void DrawRow(ItemData item)
    {
        Rect row = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
        if (item == current) EditorGUI.DrawRect(row, new Color(0.24f, 0.37f, 0.59f, 0.45f));

        Rect toggleRect = new Rect(row.x + 4f, row.y + 13f, 16f, 16f);
        bool on = ticked.Contains(item);
        bool now = EditorGUI.Toggle(toggleRect, on);
        if (now != on)
        {
            if (now) ticked.Add(item);
            else ticked.Remove(item);
        }

        Rect thumb = new Rect(row.x + 24f, row.y + 3f, 36f, 36f);
        EditorGUI.DrawRect(thumb, LiningColor);
        DrawSprite(thumb, item.icon);

        Vector2Int size = item.GetSize(0);
        Rect nameRect = new Rect(thumb.xMax + 6f, row.y + 4f, row.xMax - thumb.xMax - 8f, 18f);
        GUI.Label(nameRect, $"{Label(item)}  ({size.x}x{size.y})", EditorStyles.boldLabel);

        statusStyle.normal.textColor = Status(item, out string status);
        GUI.Label(new Rect(nameRect.x, row.y + 22f, nameRect.width, 16f), status, statusStyle);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition) && !toggleRect.Contains(e.mousePosition))
        {
            current = item;
            GUI.FocusControl(null);
            QueuePreview();
            e.Use();
        }
    }

    private Color Status(ItemData item, out string text)
    {
        if (item.PreviewSource == null) { text = "ไม่มีโมเดล (ใส่ World / Preview Prefab ก่อน)"; return new Color(1f, 0.45f, 0.4f); }
        if (item.isBag) { text = "กระเป๋า"; return Color.gray; }
        if (item.icon == null) { text = "ยังไม่มีไอคอน"; return new Color(1f, 0.7f, 0.3f); }
        if (IsToolIcon(item.icon)) { text = "ไอคอนจากเครื่องมือนี้"; return new Color(0.55f, 0.85f, 0.5f); }
        text = "รูปทำมือ (ฝ่ายอาร์ต)";
        return new Color(0.5f, 0.75f, 1f);
    }

    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;

        Texture2D tex = sprite.texture;
        Rect tr = sprite.textureRect;
        Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);

        float scale = Mathf.Min(rect.width / tr.width, rect.height / tr.height);
        Vector2 size = new Vector2(tr.width, tr.height) * scale;
        Rect fit = new Rect(rect.center - size * 0.5f, size);
        GUI.DrawTextureWithTexCoords(fit, tex, uv, true);
    }

    private void DrawRight()
    {
        EditorGUILayout.BeginVertical();
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

        EditorGUI.BeginChangeCheck();
        profile = (ItemIconProfile)EditorGUILayout.ObjectField("ไฟล์ตั้งค่า", profile, typeof(ItemIconProfile), false);
        if (EditorGUI.EndChangeCheck()) QueuePreview();

        if (profile == null)
        {
            EditorGUILayout.HelpBox("ไม่มีไฟล์ตั้งค่า", MessageType.Warning);
        }
        else
        {
            GUILayout.Space(6f);
            if (current == null)
            {
                EditorGUILayout.HelpBox("คลิกชื่อไอเทมทางซ้าย เพื่อดูตัวอย่างและปรับมุม", MessageType.Info);
            }
            else
            {
                DrawPreview();
                DrawItemSettings();
            }

            GUILayout.Space(10f);
            DrawGenerateButtons();

            GUILayout.Space(10f);
            showGlobal = EditorGUILayout.Foldout(showGlobal, "ตั้งค่ารวม (ใช้กับทุกไอเทม)", true);
            if (showGlobal) DrawGlobalSettings();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawPreview()
    {
        Vector2Int size = current.GetSize(0);
        float maxW = Mathf.Max(120f, position.width - 340f);
        float cell = Mathf.Min(maxW / size.x, 300f / size.y, 150f);
        float w = size.x * cell, h = size.y * cell;

        Rect area = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
        EditorGUI.DrawRect(area, LiningColor);

        var cells = new HashSet<Vector2Int>(current.GetCells(0));
        for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                if (cells.Contains(new Vector2Int(x, y))) continue;
                EditorGUI.DrawRect(new Rect(area.x + x * cell, area.y + y * cell, cell, cell), new Color(0f, 0f, 0f, 0.4f));
            }

        for (int x = 0; x <= size.x; x++)
            EditorGUI.DrawRect(new Rect(area.x + x * cell - 0.5f, area.y, 1f, h), StitchColor);
        for (int y = 0; y <= size.y; y++)
            EditorGUI.DrawRect(new Rect(area.x, area.y + y * cell - 0.5f, w, 1f), StitchColor);

        if (preview != null && previewItem == current)
            GUI.DrawTexture(area, preview, ScaleMode.StretchToFill, true);

        EditorGUILayout.LabelField(previewNote, EditorStyles.miniLabel);
    }

    private void DrawItemSettings()
    {
        ItemIconProfile.ItemSettings saved = profile.Find(current);
        ItemIconProfile.ItemSettings s = saved ?? SettingsFor(current);

        Vector3 rotation = s.rotation;
        bool autoRoll = s.autoRoll;
        float roll = s.roll;
        float zoom = s.zoom;
        Vector2 offset = s.offset;
        bool changed = false;

        EditorGUILayout.LabelField(Label(current), EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        rotation = EditorGUILayout.Vector3Field("หมุนโมเดล", rotation);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Y -45", EditorStyles.miniButtonLeft)) { rotation.y -= 45f; changed = true; }
        if (GUILayout.Button("Y +45", EditorStyles.miniButtonMid)) { rotation.y += 45f; changed = true; }
        if (GUILayout.Button("X +90", EditorStyles.miniButtonMid)) { rotation.x += 90f; changed = true; }
        if (GUILayout.Button("Z +90", EditorStyles.miniButtonMid)) { rotation.z += 90f; changed = true; }
        if (GUILayout.Button("มุมเดียวกับรูปถ่าย", EditorStyles.miniButtonRight)) { rotation = current.previewRotation; changed = true; }
        EditorGUILayout.EndHorizontal();

        autoRoll = EditorGUILayout.Toggle("เอียงให้เต็มช่องเอง", autoRoll);
        using (new EditorGUI.DisabledScope(autoRoll))
            roll = EditorGUILayout.Slider("เอียง (องศา)", roll, -180f, 180f);
        zoom = EditorGUILayout.Slider("ขยาย", zoom, 0.5f, 1.5f);
        offset.x = EditorGUILayout.Slider("เลื่อน ซ้าย-ขวา", offset.x, -0.5f, 0.5f);
        offset.y = EditorGUILayout.Slider("เลื่อน ขึ้น-ลง", offset.y, -0.5f, 0.5f);
        changed |= EditorGUI.EndChangeCheck();

        if (changed)
        {
            Undo.RecordObject(profile, "Item Icon Settings");
            s = profile.GetOrAdd(current);
            s.rotation = rotation;
            s.autoRoll = autoRoll;
            s.roll = roll;
            s.zoom = zoom;
            s.offset = offset;
            EditorUtility.SetDirty(profile);
            QueuePreview();
        }

        using (new EditorGUI.DisabledScope(saved == null))
        {
            if (GUILayout.Button("รีเซ็ตไอเทมนี้", GUILayout.Width(120f)))
            {
                Undo.RecordObject(profile, "Reset Item Icon Settings");
                profile.ResetItem(saved);
                EditorUtility.SetDirty(profile);
                QueuePreview();
            }
        }
    }

    private void DrawGenerateButtons()
    {
        using (new EditorGUI.DisabledScope(current == null))
        {
            if (GUILayout.Button("สร้างไอคอนอันนี้", GUILayout.Height(30f)))
            {
                ItemData item = current;
                EditorApplication.delayCall += () => GenerateSingle(item);
            }
        }

        using (new EditorGUI.DisabledScope(ticked.Count == 0))
        {
            if (GUILayout.Button($"สร้างที่ติ๊กไว้ ({ticked.Count} ชิ้น)", GUILayout.Height(30f)))
            {
                var list = new List<ItemData>(ticked);
                EditorApplication.delayCall += () => Generate(list, false);
            }
        }
    }

    private void DrawGlobalSettings()
    {
        if (serializedProfile == null || serializedProfile.targetObject != profile)
            serializedProfile = new SerializedObject(profile);

        SerializedObject o = serializedProfile;
        o.Update();

        EditorGUILayout.BeginHorizontal();
        SerializedProperty folder = o.FindProperty("outputFolder");
        EditorGUILayout.PropertyField(folder, new GUIContent("โฟลเดอร์ที่เซฟ"));
        if (GUILayout.Button("เลือก", GUILayout.Width(50f)))
        {
            string picked = EditorUtility.OpenFolderPanel("เลือกโฟลเดอร์เก็บไอคอน", folder.stringValue, "");
            if (!string.IsNullOrEmpty(picked))
            {
                picked = picked.Replace('\\', '/');
                string data = Application.dataPath.Replace('\\', '/');
                if (picked.StartsWith(data)) folder.stringValue = "Assets" + picked.Substring(data.Length);
                else ShowNotification(new GUIContent("ต้องเลือกโฟลเดอร์ที่อยู่ใน Assets"));
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.IntPopup(o.FindProperty("pixelsPerCell"), PpcLabels, PpcValues, new GUIContent("ความละเอียดต่อช่อง (px)"));
        EditorGUILayout.IntSlider(o.FindProperty("supersample"), 1, 4, new GUIContent("ความเนียนขอบ"));
        EditorGUILayout.Slider(o.FindProperty("padding"), 0f, 0.3f, new GUIContent("ขอบเว้นรอบของ"));
        EditorGUILayout.Slider(o.FindProperty("cameraElevation"), 0f, 90f, new GUIContent("มุมกล้องก้มลง (องศา)"));

        GUILayout.Space(6f);
        EditorGUILayout.LabelField("แสง", EditorStyles.boldLabel);
        EditorGUILayout.Slider(o.FindProperty("brightness"), -3f, 3f, new GUIContent("ความสว่าง"));
        ColorAndIntensity(o, "ไฟหลัก (ซ้ายบน)", "keyColor", "keyIntensity", 10f);
        ColorAndIntensity(o, "ไฟเสริม (ขวา)", "fillColor", "fillIntensity", 5f);
        ColorAndIntensity(o, "ไฟขอบ (ข้างหลัง)", "rimColor", "rimIntensity", 8f);
        EditorGUILayout.Slider(o.FindProperty("ambient"), 0f, 3f, new GUIContent("แสงรอบข้าง"));
        EditorGUILayout.PropertyField(o.FindProperty("selfShadows"), new GUIContent("เงาบนตัวของ"));

        GUILayout.Space(6f);
        EditorGUILayout.LabelField("เงาตกใต้ไอคอน", EditorStyles.boldLabel);
        SerializedProperty dropShadow = o.FindProperty("dropShadow");
        EditorGUILayout.PropertyField(dropShadow, new GUIContent("เปิดเงา"));
        using (new EditorGUI.DisabledScope(!dropShadow.boolValue))
        {
            EditorGUILayout.Slider(o.FindProperty("shadowOpacity"), 0f, 1f, new GUIContent("ความเข้ม"));
            EditorGUILayout.PropertyField(o.FindProperty("shadowOffset"), new GUIContent("ระยะเงา (x ขวา, y ลง)"));
            EditorGUILayout.Slider(o.FindProperty("shadowBlur"), 0f, 0.1f, new GUIContent("ความฟุ้ง"));
        }

        GUILayout.Space(6f);
        EditorGUILayout.LabelField("ตอนกดสร้าง", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(o.FindProperty("assignToItem"), new GUIContent("ใส่ไอคอนลง ItemData ให้เลย"));
        EditorGUILayout.PropertyField(o.FindProperty("keepHandmadeIcons"), new GUIContent("ไม่ทับรูปที่ฝ่ายอาร์ตทำ"));
        EditorGUILayout.PropertyField(o.FindProperty("skipBags"), new GUIContent("ข้ามกระเป๋า"));

        if (o.ApplyModifiedProperties()) QueuePreview();
    }

    private static void ColorAndIntensity(SerializedObject o, string label, string color, string intensity, float max)
    {
        EditorGUILayout.PropertyField(o.FindProperty(color), new GUIContent(label));
        EditorGUI.indentLevel++;
        EditorGUILayout.Slider(o.FindProperty(intensity), 0f, max, new GUIContent("ความแรง"));
        EditorGUI.indentLevel--;
    }

    // ---------- Preview ----------

    private IconStage Stage()
    {
        if (stage == null || !stage.IsValid)
        {
            stage?.Dispose();
            stage = new IconStage();
        }
        return stage;
    }

    private void QueuePreview()
    {
        if (previewQueued) return;
        previewQueued = true;
        EditorApplication.delayCall += RenderPreview;
    }

    private void RenderPreview()
    {
        previewQueued = false;
        if (this == null || profile == null || current == null) return;

        try
        {
            ItemIconProfile.ItemSettings s = SettingsFor(current);
            Texture2D tex = Stage().Render(current, s, profile, PreviewPixelsPerCell, 2, out float roll, out string error);

            if (preview != null) DestroyImmediate(preview);
            preview = tex;
            previewItem = current;
            previewNote = tex == null ? error : s.autoRoll ? $"เครื่องมือเอียงให้ {roll:0}°" : "";
        }
        catch (Exception e)
        {
            previewNote = "เรนเดอร์ไม่สำเร็จ: " + e.Message;
            Debug.LogException(e);
        }

        Repaint();
    }

    // ---------- Generate ----------

    private void GenerateSingle(ItemData item)
    {
        if (item == null || profile == null) return;

        bool force = false;
        if (IsHandmade(item))
        {
            if (!EditorUtility.DisplayDialog("Item Icons",
                    $"{Label(item)} มีรูปจากฝ่ายอาร์ตอยู่แล้ว จะสร้างไอคอนใหม่ทับไหม?", "ทับเลย", "ยกเลิก"))
                return;
            force = true;
        }

        Generate(new List<ItemData> { item }, force);
    }

    private string SkipReason(ItemData item, bool force)
    {
        if (item.PreviewSource == null) return "ไม่มีโมเดล";
        if (force) return null;
        if (profile.skipBags && item.isBag) return "เป็นกระเป๋า";
        if (profile.keepHandmadeIcons && IsHandmade(item)) return "มีรูปจากฝ่ายอาร์ตแล้ว";
        return null;
    }

    private void Generate(List<ItemData> items, bool force)
    {
        if (profile == null) return;

        string folder = profile.outputFolder.Replace('\\', '/').TrimEnd('/');
        if (!folder.StartsWith("Assets"))
        {
            EditorUtility.DisplayDialog("Item Icons", "โฟลเดอร์ที่เซฟต้องอยู่ใน Assets", "ตกลง");
            return;
        }
        Directory.CreateDirectory(folder);

        var written = new List<KeyValuePair<ItemData, string>>();
        var skipped = new List<string>();
        IconStage st = Stage();

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                ItemData item = items[i];
                if (item == null) continue;

                if (EditorUtility.DisplayCancelableProgressBar("Item Icons",
                        $"กำลังสร้าง {Label(item)} ({i + 1}/{items.Count})", (float)i / items.Count))
                    break;

                string reason = SkipReason(item, force);
                if (reason != null)
                {
                    skipped.Add($"{Label(item)}: {reason}");
                    continue;
                }

                Texture2D tex = st.Render(item, SettingsFor(item), profile, profile.pixelsPerCell, profile.supersample, out _, out string error);
                if (tex == null)
                {
                    skipped.Add($"{Label(item)}: {error}");
                    continue;
                }

                string path = $"{folder}/{FileName(item)}_Icon.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                DestroyImmediate(tex);
                written.Add(new KeyValuePair<ItemData, string>(item, path));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var pair in written) ConfigureImporter(pair.Value);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        if (profile.assignToItem)
        {
            foreach (var pair in written)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pair.Value);
                if (sprite == null) continue;
                Undo.RecordObject(pair.Key, "Assign Item Icon");
                pair.Key.icon = sprite;
                EditorUtility.SetDirty(pair.Key);
            }
        }
        AssetDatabase.SaveAssets();

        Debug.Log($"[Item Icons] สร้างแล้ว {written.Count} ชิ้น → {folder}");
        if (skipped.Count > 0)
            Debug.LogWarning("[Item Icons] ข้าม " + skipped.Count + " ชิ้น:\n" + string.Join("\n", skipped));

        ShowNotification(new GUIContent($"สร้างแล้ว {written.Count} ชิ้น" + (skipped.Count > 0 ? $" (ข้าม {skipped.Count})" : "")));
        Repaint();
    }

    private static string FileName(ItemData item)
    {
        string name = string.IsNullOrEmpty(item.id) ? item.name : item.id;
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private static void ConfigureImporter(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(settings);

        ti.SaveAndReimport();
    }
}

// ฉากแยกสำหรับถ่ายไอคอน — มีกล้อง ไฟ 3 ดวง และ Volume ของตัวเอง ฉากเกมกับ Volume ในฉากไม่มีผล
internal sealed class IconStage : IDisposable
{
    private static readonly Vector3 Origin = new Vector3(0f, -10000f, 0f);
    private const int ProbeSize = 256;

    private Scene scene;
    private readonly GameObject holder;
    private readonly Camera cam;
    private readonly HDAdditionalCameraData hd;
    private readonly Light key;
    private readonly Light fill;
    private readonly Light rim;
    private readonly VolumeProfile volumeProfile;
    private readonly Exposure exposure;
    private readonly GradientSky sky;
    private readonly HDShadowSettings shadowSettings;

    public bool IsValid => scene.IsValid() && cam != null && holder != null;

    public IconStage()
    {
        scene = EditorSceneManager.NewPreviewScene();

        holder = AddToScene(new GameObject("Model"));
        holder.SetActive(false);
        holder.transform.position = Origin;

        GameObject camGo = AddToScene(new GameObject("IconCamera"));
        cam = camGo.AddComponent<Camera>();
        hd = Ensure<HDAdditionalCameraData>(camGo);
        cam.enabled = false;
        cam.scene = scene;
        cam.orthographic = true;
        cam.cullingMask = ~0;

        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
        hd.volumeLayerMask = ~0;
        hd.customRenderingSettings = true;
        Off(FrameSettingsField.Bloom);
        Off(FrameSettingsField.Vignette);
        Off(FrameSettingsField.Tonemapping);
        Off(FrameSettingsField.ColorGrading);
        Off(FrameSettingsField.FilmGrain);
        Off(FrameSettingsField.Dithering);
        Off(FrameSettingsField.DepthOfField);
        Off(FrameSettingsField.MotionBlur);
        Off(FrameSettingsField.ChromaticAberration);
        Off(FrameSettingsField.LensDistortion);
        Off(FrameSettingsField.PaniniProjection);
        Off(FrameSettingsField.AtmosphericScattering);
        Off(FrameSettingsField.Volumetrics);
        Off(FrameSettingsField.SSR);
        Off(FrameSettingsField.SSAO);

        key = AddLight("Key");
        fill = AddLight("Fill");
        rim = AddLight("Rim");

        volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volumeProfile.hideFlags = HideFlags.HideAndDontSave;

        exposure = volumeProfile.Add<Exposure>();
        exposure.mode.Override(ExposureMode.Fixed);

        VisualEnvironment env = volumeProfile.Add<VisualEnvironment>();
        env.skyType.Override((int)SkyType.Gradient);
        env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

        sky = volumeProfile.Add<GradientSky>();

        Fog fog = volumeProfile.Add<Fog>();
        fog.enabled.Override(false);

        shadowSettings = volumeProfile.Add<HDShadowSettings>();

        GameObject volumeGo = AddToScene(new GameObject("IconVolume"));
        Volume volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10000f;
        volume.sharedProfile = volumeProfile;
    }

    public void Dispose()
    {
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);

        if (volumeProfile != null)
        {
            foreach (VolumeComponent c in volumeProfile.components)
                if (c != null) Object.DestroyImmediate(c);
            Object.DestroyImmediate(volumeProfile);
        }
    }

    public Texture2D Render(ItemData item, ItemIconProfile.ItemSettings s, ItemIconProfile p,
                            int pixelsPerCell, int supersample, out float roll, out string error)
    {
        roll = 0f;
        error = null;

        GameObject source = item.PreviewSource;
        if (source == null)
        {
            error = "ไม่มีโมเดล";
            return null;
        }

        holder.SetActive(false);
        GameObject model = Object.Instantiate(source, holder.transform);

        try
        {
            KeepVisualsOnly(model);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(s.rotation);
            holder.SetActive(true);
            foreach (LODGroup lod in model.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);

            if (!TryGetBounds(model, out Bounds bounds))
            {
                error = "โมเดลไม่มี Mesh ให้ถ่าย";
                return null;
            }

            float radius = Mathf.Max(bounds.extents.magnitude, 0.001f);
            float distance = radius * 4f;
            ApplyLook(p, radius);

            // 1) ถ่ายเล็กๆ ก่อน เพื่อดูว่าโมเดลกินพื้นที่บนจอตรงไหนจริงๆ
            Quaternion baseRotation = Quaternion.Euler(p.cameraElevation, 0f, 0f);
            float probeHalf = radius * 1.1f;
            Place(baseRotation, bounds.center, probeHalf, distance, radius, 1f);
            if (!RenderPair(ProbeSize, ProbeSize, true, out Color[] probeBlack, out Color[] probeWhite) ||
                !Matte(probeBlack, probeWhite, ProbeSize, ProbeSize, out float[] probeAlpha, out _))
            {
                error = "สีพื้นหลังไม่เปลี่ยน — HDRP ไม่ได้เรนเดอร์กล้องนี้";
                return null;
            }

            List<Vector3> outline = Outline(probeAlpha, baseRotation, probeHalf);
            if (outline.Count == 0)
            {
                error = "กล้องมองไม่เห็นโมเดล";
                return null;
            }

            // 2) หามุมเอียงที่ทำให้ของใหญ่ที่สุดในช่อง
            Vector2Int size = item.GetSize(0);
            float pad = p.padding;
            if (p.dropShadow)
                pad += Mathf.Max(Mathf.Abs(p.shadowOffset.x), Mathf.Abs(p.shadowOffset.y)) + p.shadowBlur;
            float availW = Mathf.Max(0.1f, size.x - pad * 2f);
            float availH = Mathf.Max(0.1f, size.y - pad * 2f);
            float margin = probeHalf * 2f / ProbeSize;

            float bestScore = float.MaxValue, bestK = 1f, bestRoll = 0f;
            Vector2 bestCenter = Vector2.zero;
            float from = s.autoRoll ? -90f : s.roll;
            float to = s.autoRoll ? 90f : s.roll;

            for (float r = from; r <= to + 0.001f; r += 3f)
            {
                Quaternion rot = baseRotation * Quaternion.Euler(0f, 0f, r);
                Vector3 right = rot * Vector3.right, up = rot * Vector3.up;

                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (Vector3 d in outline)
                {
                    float x = Vector3.Dot(d, right), y = Vector3.Dot(d, up);
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }

                float k = Mathf.Max((maxX - minX + margin * 2f) / availW, (maxY - minY + margin * 2f) / availH);
                float score = k * (1f + 0.03f * Mathf.Abs(r) / 90f);   // ใกล้เคียงกัน = เอาแบบตั้งตรง
                if (score < bestScore)
                {
                    bestScore = score;
                    bestK = k;
                    bestRoll = r;
                    bestCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
                }
                if (!s.autoRoll) break;
            }
            roll = bestRoll;

            // 3) ถ่ายจริงขนาดเท่าช่อง
            float unitsPerCell = bestK / Mathf.Max(0.05f, s.zoom);
            Quaternion finalRotation = baseRotation * Quaternion.Euler(0f, 0f, bestRoll);
            Vector3 center = bounds.center
                             + finalRotation * Vector3.right * (bestCenter.x - s.offset.x * size.x * unitsPerCell)
                             + finalRotation * Vector3.up * (bestCenter.y + s.offset.y * size.y * unitsPerCell);

            int w = size.x * pixelsPerCell, h = size.y * pixelsPerCell;
            int ss = Mathf.Clamp(supersample, 1, 4);
            Place(finalRotation, center, size.y * unitsPerCell * 0.5f, distance, radius, (float)w / h);

            if (!RenderPair(w * ss, h * ss, true, out Color[] black, out Color[] white) ||
                !Matte(black, white, w * ss, h * ss, out float[] alpha, out Color background))
            {
                error = "สีพื้นหลังไม่เปลี่ยน — HDRP ไม่ได้เรนเดอร์กล้องนี้";
                return null;
            }

            Color[] image = Downsample(black, alpha, background, w, h, ss);
            if (p.dropShadow && p.shadowOpacity > 0f) AddDropShadow(image, w, h, p, pixelsPerCell);
            return ToTexture(image, w, h);
        }
        finally
        {
            Object.DestroyImmediate(model);
            holder.SetActive(false);
        }
    }

    // ---------- Setup ----------

    private GameObject AddToScene(GameObject go)
    {
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static T Ensure<T>(GameObject go) where T : Component =>
        go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    private void Off(FrameSettingsField field)
    {
        hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
        hd.renderingPathCustomFrameSettings.SetEnabled(field, false);
    }

    private Light AddLight(string name)
    {
        GameObject go = AddToScene(new GameObject(name));
        Light light = go.AddComponent<Light>();
        Ensure<HDAdditionalLightData>(go);
        light.type = LightType.Directional;
        light.useColorTemperature = false;
        light.shadows = LightShadows.None;
        return light;
    }

    private void ApplyLook(ItemIconProfile p, float radius)
    {
        exposure.fixedExposure.Override(-p.brightness);

        Quaternion view = Quaternion.Euler(p.cameraElevation, 0f, 0f);
        SetLight(key, p.keyColor, p.keyIntensity, view * Quaternion.Euler(40f, 35f, 0f));
        SetLight(fill, p.fillColor, p.fillIntensity, view * Quaternion.Euler(10f, -50f, 0f));
        SetLight(rim, p.rimColor, p.rimIntensity, view * Quaternion.Euler(35f, 180f, 0f));
        key.shadows = p.selfShadows ? LightShadows.Soft : LightShadows.None;

        float a = Mathf.Max(0f, p.ambient);
        sky.top.Override(new Color(0.32f * a, 0.30f * a, 0.27f * a, 1f));
        sky.middle.Override(new Color(0.22f * a, 0.20f * a, 0.18f * a, 1f));
        sky.bottom.Override(new Color(0.10f * a, 0.09f * a, 0.08f * a, 1f));

        shadowSettings.maxShadowDistance.Override(Mathf.Max(1f, radius * 12f));
    }

    private static void SetLight(Light light, Color color, float intensity, Quaternion rotation)
    {
        light.color = color;
        light.intensity = intensity;
        light.enabled = intensity > 0f;
        light.transform.rotation = rotation;
    }

    private static void KeepVisualsOnly(GameObject model)
    {
        // ปิดไว้ก่อนเปิดใช้งาน สคริปต์เกมจะได้ไม่ทำงานในฉากถ่ายรูป
        MonoBehaviour[] scripts = model.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = scripts.Length - 1; i >= 0; i--)
            if (scripts[i] != null) Object.DestroyImmediate(scripts[i]);

        foreach (Behaviour b in model.GetComponentsInChildren<Behaviour>(true)) b.enabled = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            r.enabled = r is MeshRenderer || r is SkinnedMeshRenderer;
    }

    private static bool TryGetBounds(GameObject model, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    private void Place(Quaternion rotation, Vector3 center, float orthoHalfHeight, float distance, float radius, float aspect)
    {
        cam.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);
        cam.orthographicSize = orthoHalfHeight;
        cam.nearClipPlane = Mathf.Max(0.001f, distance - radius * 2f);
        cam.farClipPlane = distance + radius * 2f;
        cam.aspect = aspect;
    }

    // ---------- Render ----------

    // ถ่าย 2 รอบ พื้นดำกับพื้นขาว แล้วเทียบกันเพื่อหาความโปร่งใส — ไม่ต้องพึ่งการตั้งค่า Alpha ของ HDRP
    private bool RenderPair(int w, int h, bool warmUp, out Color[] black, out Color[] white)
    {
        black = white = null;
        var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGBHalf, 24) { sRGB = false, msaaSamples = 1 };
        RenderTexture rt = RenderTexture.GetTemporary(desc);

        try
        {
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;

            if (warmUp)
            {
                hd.backgroundColorHDR = Color.black;
                cam.Render();
            }

            hd.backgroundColorHDR = Color.black;
            cam.Render();
            black = Read(rt);

            hd.backgroundColorHDR = Color.white;
            cam.Render();
            white = Read(rt);
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }
        return black != null && white != null;
    }

    private static Color[] Read(RenderTexture rt)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
        tex.Apply(false);
        RenderTexture.active = previous;

        Color[] pixels = tex.GetPixels();
        Object.DestroyImmediate(tex);
        return pixels;
    }

    private static bool Matte(Color[] black, Color[] white, int w, int h, out float[] alpha, out Color background)
    {
        alpha = null;
        background = Color.black;

        int[] corners = { 0, w - 1, (h - 1) * w, h * w - 1 };
        int best = 0;
        float bestDiff = float.MinValue;
        foreach (int c in corners)
        {
            float d = Luminance(white[c]) - Luminance(black[c]);
            if (d > bestDiff) { bestDiff = d; best = c; }
        }
        if (bestDiff < 1e-4f) return false;

        Color bb = black[best], bw = white[best];
        float ir = Inverse(bw.r - bb.r), ig = Inverse(bw.g - bb.g), ib = Inverse(bw.b - bb.b);
        float channels = (ir > 0f ? 1f : 0f) + (ig > 0f ? 1f : 0f) + (ib > 0f ? 1f : 0f);

        alpha = new float[w * h];
        for (int i = 0; i < alpha.Length; i++)
        {
            Color k = black[i], q = white[i];
            float through = ((q.r - k.r) * ir + (q.g - k.g) * ig + (q.b - k.b) * ib) / channels;
            alpha[i] = Mathf.Clamp01(1f - through);
        }

        background = bb;
        return true;
    }

    private static float Inverse(float v) => v > 1e-4f ? 1f / v : 0f;

    private static float Luminance(Color c) => c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;

    // จุดขอบของโมเดลบนรูปเล็ก แปลงเป็นระยะจริงบนระนาบกล้อง
    private static List<Vector3> Outline(float[] alpha, Quaternion rotation, float half)
    {
        var points = new List<Vector3>();
        Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
        const float solid = 0.05f;

        for (int y = 0; y < ProbeSize; y++)
            for (int x = 0; x < ProbeSize; x++)
            {
                if (alpha[y * ProbeSize + x] < solid) continue;

                bool edge = x == 0 || y == 0 || x == ProbeSize - 1 || y == ProbeSize - 1 ||
                            alpha[y * ProbeSize + x - 1] < solid || alpha[y * ProbeSize + x + 1] < solid ||
                            alpha[(y - 1) * ProbeSize + x] < solid || alpha[(y + 1) * ProbeSize + x] < solid;
                if (!edge) continue;

                float px = ((x + 0.5f) / ProbeSize * 2f - 1f) * half;
                float py = ((y + 0.5f) / ProbeSize * 2f - 1f) * half;
                points.Add(right * px + up * py);
            }
        return points;
    }

    // ย่อรูปใหญ่ลงให้ขอบเนียน เก็บเป็นสีแบบคูณ alpha แล้ว (linear)
    private static Color[] Downsample(Color[] black, float[] alpha, Color background, int w, int h, int ss)
    {
        int bigW = w * ss;
        float inv = 1f / (ss * ss);
        var result = new Color[w * h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int j = 0; j < ss; j++)
                    for (int i = 0; i < ss; i++)
                    {
                        int idx = (y * ss + j) * bigW + x * ss + i;
                        float al = alpha[idx];
                        Color k = black[idx];
                        r += Mathf.Max(0f, k.r - (1f - al) * background.r);
                        g += Mathf.Max(0f, k.g - (1f - al) * background.g);
                        b += Mathf.Max(0f, k.b - (1f - al) * background.b);
                        a += al;
                    }
                result[y * w + x] = new Color(r * inv, g * inv, b * inv, a * inv);
            }
        return result;
    }

    private static void AddDropShadow(Color[] image, int w, int h, ItemIconProfile p, int pixelsPerCell)
    {
        var alpha = new float[w * h];
        for (int i = 0; i < alpha.Length; i++) alpha[i] = image[i].a;

        float[] soft = Blur(alpha, w, h, Mathf.RoundToInt(p.shadowBlur * pixelsPerCell));
        int ox = Mathf.RoundToInt(p.shadowOffset.x * pixelsPerCell);
        int oy = Mathf.RoundToInt(p.shadowOffset.y * pixelsPerCell);
        var shade = new Color(0.012f, 0.008f, 0.004f);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = x - ox, sy = y + oy;   // แถว 0 อยู่ล่างสุด เงาที่ตกลงล่างเลยต้องอ่านจากแถวที่สูงกว่า
                if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;

                float sa = soft[sy * w + sx] * p.shadowOpacity;
                if (sa <= 0f) continue;

                int i = y * w + x;
                Color k = image[i];
                float keep = sa * (1f - k.a);
                image[i] = new Color(k.r + shade.r * keep, k.g + shade.g * keep, k.b + shade.b * keep, k.a + keep);
            }
    }

    private static float[] Blur(float[] source, int w, int h, int radius)
    {
        var a = (float[])source.Clone();
        if (radius <= 0) return a;

        var tmp = new float[w * h];
        float norm = 1f / (radius * 2 + 1);

        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < h; y++)
            {
                float sum = 0f;
                for (int x = -radius; x <= radius; x++) sum += x >= 0 && x < w ? a[y * w + x] : 0f;
                for (int x = 0; x < w; x++)
                {
                    tmp[y * w + x] = sum * norm;
                    int add = x + radius + 1, sub = x - radius;
                    if (add < w) sum += a[y * w + add];
                    if (sub >= 0) sum -= a[y * w + sub];
                }
            }

            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int y = -radius; y <= radius; y++) sum += y >= 0 && y < h ? tmp[y * w + x] : 0f;
                for (int y = 0; y < h; y++)
                {
                    a[y * w + x] = sum * norm;
                    int add = y + radius + 1, sub = y - radius;
                    if (add < h) sum += tmp[add * w + x];
                    if (sub >= 0) sum -= tmp[sub * w + x];
                }
            }
        }
        return a;
    }

    private static Texture2D ToTexture(Color[] image, int w, int h)
    {
        var pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color k = image[i];
            if (k.a <= 0.0001f) continue;

            float a = Mathf.Clamp01(k.a);
            pixels[i] = new Color32(
                ToByte(Mathf.LinearToGammaSpace(Mathf.Clamp01(k.r / k.a))),
                ToByte(Mathf.LinearToGammaSpace(Mathf.Clamp01(k.g / k.a))),
                ToByte(Mathf.LinearToGammaSpace(Mathf.Clamp01(k.b / k.a))),
                ToByte(a));
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.SetPixels32(pixels);
        tex.Apply(false);
        return tex;
    }

    private static byte ToByte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
}