using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// ต้องอยู่ในโฟลเดอร์ชื่อ Editor — ใช้ตอนแต่งฉากเท่านั้น ไม่ติดไปในเกมตอน build
public class DecalPainterWindow : EditorWindow
{
    [System.Serializable]
    private class Entry
    {
        public Material Material;
        public bool Enabled = true;
    }

    [System.Serializable]
    private class SaveData
    {
        public List<string> Guids = new List<string>();
        public List<bool> Enabled = new List<bool>();
        public float BrushRadius = 2f;
        public int Density = 2;
        public float Spacing = 0.4f;
        public float Size = 0.5f;
        public float MinScale = 0.8f;
        public float MaxScale = 1.2f;
        public float MinOpacity = 0.85f;
        public float MaxOpacity = 1f;
        public bool RandomRotation = true;
        public bool AlignToSurface = true;
        public float DrawDistance = 40f;
        public float Depth = 0.5f;
        public int PaintLayers = Physics.DefaultRaycastLayers;
    }

    private const string ContainerName = "Decals_Painted";

    private readonly List<Entry> _entries = new List<Entry>();
    private SaveData _s = new SaveData();

    private bool _painting;
    private bool _stroking;
    private bool _hasHit;
    private RaycastHit _hit;
    private Vector3 _lastStampPoint;
    private int _undoGroup;
    private Vector2 _scroll;

    private static string PrefsKey => "DecalPainter_" + PlayerSettings.productName;

    [MenuItem("Tools/Decal Painter")]
    private static void Open() => GetWindow<DecalPainterWindow>("Decal Painter");

    private void OnEnable()
    {
        Load();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        Save();
        SceneView.duringSceneGui -= OnSceneGUI;
        if (_painting)
            Tools.hidden = false;
        _painting = false;
    }

    // ซ่อนลูกศร Move/Rotate ของ object ที่เลือกอยู่ตอน Paint ไม่งั้นคลิกโดนลูกศรแทน
    private void SetPainting(bool on)
    {
        _painting = on;
        _stroking = false;
        Tools.hidden = on;
        Repaint();
        SceneView.RepaintAll();
    }

    // ---------- หน้าต่าง ----------

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "คลิกลากใน Scene = โปรย decal\n" +
            "Shift + ลาก = ลบ\n" +
            "[ กับ ] = ย่อ/ขยายแปรง\n" +
            "Ctrl + Z = ย้อนทีละเส้นที่ลาก\n" +
            "Esc = หยุด Paint\n" +
            "พื้นต้องมี Collider (terrain มีอยู่แล้ว)", MessageType.Info);

        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = _painting ? new Color(1f, 0.45f, 0.45f) : new Color(0.45f, 1f, 0.55f);
        if (GUILayout.Button(_painting ? "■  หยุด Paint" : "●  เริ่ม Paint", GUILayout.Height(36)))
            SetPainting(!_painting);
        GUI.backgroundColor = oldColor;

        EditorGUILayout.Space(8);
        DrawMaterialList();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("แปรง", EditorStyles.boldLabel);
        _s.BrushRadius = EditorGUILayout.Slider(new GUIContent("ขนาดแปรง (ม.)", "รัศมีวงแปรง"), _s.BrushRadius, 0.2f, 15f);
        _s.Density = EditorGUILayout.IntSlider(new GUIContent("จำนวนต่อครั้ง", "โปรยกี่อันทุกครั้งที่แปรงขยับ"), _s.Density, 1, 15);
        _s.Spacing = EditorGUILayout.Slider(new GUIContent("ระยะห่างขั้นต่ำ (ม.)", "กัน decal ซ้อนกันเป็นกอง\nยิ่งมาก ยิ่งโปรยห่าง"), _s.Spacing, 0f, 5f);
        _s.PaintLayers = LayerMaskField(new GUIContent("แปะได้บน Layer", "เลือกเฉพาะพื้น จะได้ไม่ไปแปะบนบ้านหรือต้นไม้"), _s.PaintLayers);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Decal", EditorStyles.boldLabel);
        _s.Size = EditorGUILayout.Slider(new GUIContent("ขนาด (ม.)", "ด้านที่ยาวที่สุดของรูป\nด้านสั้นคำนวณจากสัดส่วนรูปให้เอง"), _s.Size, 0.05f, 5f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent("สุ่มขนาด (เท่า)", "คูณกับขนาด ให้แต่ละอันไม่เท่ากัน"));
        _s.MinScale = EditorGUILayout.FloatField(_s.MinScale);
        _s.MaxScale = EditorGUILayout.FloatField(_s.MaxScale);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent("สุ่มความเข้ม", "0 = ใส / 1 = ชัดเต็มที่"));
        _s.MinOpacity = EditorGUILayout.FloatField(_s.MinOpacity);
        _s.MaxOpacity = EditorGUILayout.FloatField(_s.MaxOpacity);
        EditorGUILayout.EndHorizontal();
        _s.RandomRotation = EditorGUILayout.Toggle(new GUIContent("สุ่มหมุน", "หมุนแต่ละอันไม่ซ้ำกัน ช่วยให้ดูไม่เป็นลาย"), _s.RandomRotation);
        _s.AlignToSurface = EditorGUILayout.Toggle(new GUIContent("แนบตามพื้นเอียง", "ปิด = ฉายตรงลงล่างเสมอ"), _s.AlignToSurface);
        _s.Depth = EditorGUILayout.Slider(new GUIContent("ความลึกการฉาย (ม.)", "พื้นขรุขระมากให้เพิ่ม\nมากไปจะไปติดของที่อยู่ใกล้ๆ ด้วย"), _s.Depth, 0.05f, 3f);
        _s.DrawDistance = EditorGUILayout.Slider(new GUIContent("Draw Distance (ม.)", "ไกลกว่านี้ไม่วาด ประหยัดเครื่อง"), _s.DrawDistance, 5f, 300f);

        EditorGUILayout.Space(8);
        Transform container = FindContainer(false);
        EditorGUILayout.LabelField($"Decal ในฉากตอนนี้: {(container != null ? container.childCount : 0)} อัน");
        using (new EditorGUI.DisabledScope(container == null))
        {
            if (GUILayout.Button("เลือก Decals_Painted ใน Hierarchy") && container != null)
                Selection.activeTransform = container;

            if (GUILayout.Button("อัปเดต Draw Distance ให้ทุกอันในฉาก") && container != null)
                ApplyDrawDistanceToAll(container);
        }

        if (EditorGUI.EndChangeCheck())
        {
            _s.MinScale = Mathf.Max(0.01f, _s.MinScale);
            _s.MaxScale = Mathf.Max(_s.MinScale, _s.MaxScale);
            _s.MinOpacity = Mathf.Clamp01(_s.MinOpacity);
            _s.MaxOpacity = Mathf.Clamp(_s.MaxOpacity, _s.MinOpacity, 1f);
            Save();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawMaterialList()
    {
        EditorGUILayout.LabelField("Decal ที่จะโปรย (ติ๊ก = ใช้ / สุ่มจากที่ติ๊กไว้)", EditorStyles.boldLabel);

        Rect drop = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "ลาก Material decal มาวางตรงนี้\n(ลากทีเดียวหลายอันได้)", EditorStyles.helpBox);
        HandleDrop(drop);

        int removeIndex = -1;
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            EditorGUILayout.BeginHorizontal();
            entry.Enabled = EditorGUILayout.Toggle(entry.Enabled, GUILayout.Width(18));
            entry.Material = (Material)EditorGUILayout.ObjectField(entry.Material, typeof(Material), false);
            if (GUILayout.Button("X", GUILayout.Width(22)))
                removeIndex = i;
            EditorGUILayout.EndHorizontal();

            if (entry.Material != null && !IsDecalMaterial(entry.Material))
                EditorGUILayout.HelpBox($"{entry.Material.name} ไม่ได้ใช้ shader HDRP/Decal จะถูกข้าม", MessageType.Warning);
        }

        if (removeIndex >= 0)
        {
            _entries.RemoveAt(removeIndex);
            Save();
        }
    }

    private void HandleDrop(Rect area)
    {
        Event e = Event.current;
        if ((e.type != EventType.DragUpdated && e.type != EventType.DragPerform) || !area.Contains(e.mousePosition))
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (Object obj in DragAndDrop.objectReferences)
            {
                if (obj is Material mat && !_entries.Exists(x => x.Material == mat))
                    _entries.Add(new Entry { Material = mat });
            }
            Save();
        }
        e.Use();
    }

    private static int LayerMaskField(GUIContent label, int mask)
    {
        int concatenated = InternalEditorUtility.LayerMaskToConcatenatedLayersMask(mask);
        concatenated = EditorGUILayout.MaskField(label, concatenated, InternalEditorUtility.layers);
        return InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(concatenated);
    }

    // ---------- หน้า Scene ----------

    private void OnSceneGUI(SceneView view)
    {
        if (!_painting)
            return;

        Event e = Event.current;
        int id = GUIUtility.GetControlID("DecalPainter".GetHashCode(), FocusType.Passive);

        if (e.type == EventType.Layout)
        {
            // กันไม่ให้คลิกแล้วไปเลือก object อื่นแทน
            HandleUtility.AddDefaultControl(id);
            return;
        }

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                SetPainting(false);
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket)
            {
                float step = e.keyCode == KeyCode.LeftBracket ? 0.85f : 1.15f;
                _s.BrushRadius = Mathf.Clamp(_s.BrushRadius * step, 0.2f, 15f);
                Repaint();
                e.Use();
            }
        }

        UpdateHit(e.mousePosition);

        if (e.type == EventType.Repaint && _hasHit)
            DrawBrush(e.shift);

        if (e.type == EventType.MouseMove)
            view.Repaint();

        // Alt = หมุนกล้อง / ปุ่มขวา = บินกล้อง ปล่อยให้ Unity ทำงานตามปกติ
        if (e.alt || e.button != 0)
            return;

        switch (e.type)
        {
            case EventType.MouseDown:
                Physics.SyncTransforms();
                UpdateHit(e.mousePosition);
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(e.shift ? "Erase Decals" : "Paint Decals");
                _undoGroup = Undo.GetCurrentGroup();
                GUIUtility.hotControl = id;
                _stroking = true;
                Apply(view, e.shift, true);
                e.Use();
                break;

            case EventType.MouseDrag:
                if (_stroking)
                {
                    Apply(view, e.shift, false);
                    view.Repaint();
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (_stroking)
                {
                    _stroking = false;
                    if (GUIUtility.hotControl == id)
                        GUIUtility.hotControl = 0;
                    Undo.CollapseUndoOperations(_undoGroup);
                    Save();
                    e.Use();
                }
                break;
        }
    }

    private void UpdateHit(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        _hasHit = Physics.Raycast(ray, out _hit, 5000f, _s.PaintLayers, QueryTriggerInteraction.Ignore);
    }

    private void DrawBrush(bool erase)
    {
        Color color = erase ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.5f);
        Handles.color = new Color(color.r, color.g, color.b, 0.12f);
        Handles.DrawSolidDisc(_hit.point, _hit.normal, _s.BrushRadius);
        Handles.color = color;
        Handles.DrawWireDisc(_hit.point, _hit.normal, _s.BrushRadius, 2f);
    }

    private void Apply(SceneView view, bool erase, bool firstStamp)
    {
        if (!_hasHit)
            return;

        if (erase)
        {
            Erase(_hit.point);
            Repaint();
            return;
        }

        // ลากเร็วๆ ไม่ให้โปรยถี่จนเป็นกอง — ต้องขยับไปครึ่งแปรงก่อนค่อยโปรยรอบใหม่
        if (!firstStamp && Vector3.Distance(_hit.point, _lastStampPoint) < Mathf.Max(0.05f, _s.BrushRadius * 0.5f))
            return;

        List<Material> materials = GetActiveMaterials();
        if (materials.Count == 0)
        {
            view.ShowNotification(new GUIContent("ยังไม่ได้ติ๊ก Material decal ในหน้าต่าง Decal Painter"));
            return;
        }

        _lastStampPoint = _hit.point;
        Stamp(_hit, materials);
        Repaint();
    }

    private void Stamp(RaycastHit center, List<Material> materials)
    {
        Transform container = FindContainer(true);
        var placed = new List<Vector3>();
        for (int i = 0; i < container.childCount; i++)
            placed.Add(container.GetChild(i).position);

        Vector3 n = center.normal;
        Vector3 t1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
        Vector3 t2 = Vector3.Cross(n, t1);
        float lift = _s.BrushRadius + 1f;

        for (int i = 0; i < _s.Density; i++)
        {
            Vector2 r = Random.insideUnitCircle * _s.BrushRadius;
            Vector3 origin = center.point + t1 * r.x + t2 * r.y + n * lift;

            if (!Physics.Raycast(origin, -n, out RaycastHit hit, lift * 2f, _s.PaintLayers, QueryTriggerInteraction.Ignore))
                continue;
            if (IsTooClose(hit.point, placed))
                continue;

            Material mat = materials[Random.Range(0, materials.Count)];
            CreateDecal(mat, hit, container);
            placed.Add(hit.point);
        }
    }

    private bool IsTooClose(Vector3 point, List<Vector3> placed)
    {
        if (_s.Spacing <= 0f)
            return false;

        float min = _s.Spacing * _s.Spacing;
        foreach (Vector3 p in placed)
        {
            if ((p - point).sqrMagnitude < min)
                return true;
        }
        return false;
    }

    private void CreateDecal(Material mat, RaycastHit hit, Transform container)
    {
        Vector3 n = _s.AlignToSurface ? hit.normal : Vector3.up;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.forward, n);
        if (up.sqrMagnitude < 0.001f)
            up = Vector3.ProjectOnPlane(Vector3.right, n);

        // DecalProjector ฉายไปทางแกน Z ของตัวเอง เลยหันแกน Z ทิ่มลงพื้น
        Quaternion rot = Quaternion.LookRotation(-n, up);
        if (_s.RandomRotation)
            rot = Quaternion.AngleAxis(Random.Range(0f, 360f), n) * rot;

        var go = new GameObject("Decal_" + mat.name);
        go.transform.SetParent(container, false);
        go.transform.SetPositionAndRotation(hit.point, rot);

        var projector = go.AddComponent<DecalProjector>();
        projector.material = mat;

        Vector2 aspect = GetAspect(mat);
        float size = _s.Size * Random.Range(_s.MinScale, _s.MaxScale);
        projector.size = new Vector3(size * aspect.x, size * aspect.y, _s.Depth);
        projector.pivot = Vector3.zero;
        projector.drawDistance = _s.DrawDistance;
        projector.fadeScale = 0.9f;
        projector.fadeFactor = Random.Range(_s.MinOpacity, _s.MaxOpacity);

        Undo.RegisterCreatedObjectUndo(go, "Paint Decals");
    }

    private void Erase(Vector3 point)
    {
        Transform container = FindContainer(false);
        if (container == null)
            return;

        float r2 = _s.BrushRadius * _s.BrushRadius;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if ((child.position - point).sqrMagnitude <= r2)
                Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private void ApplyDrawDistanceToAll(Transform container)
    {
        DecalProjector[] projectors = container.GetComponentsInChildren<DecalProjector>(true);
        Undo.RecordObjects(projectors, "Update Decal Draw Distance");
        foreach (DecalProjector p in projectors)
            p.drawDistance = _s.DrawDistance;
    }

    // ---------- ตัวช่วย ----------

    private List<Material> GetActiveMaterials()
    {
        var list = new List<Material>();
        foreach (Entry entry in _entries)
        {
            if (entry.Enabled && entry.Material != null && IsDecalMaterial(entry.Material))
                list.Add(entry.Material);
        }
        return list;
    }

    // รองรับทั้ง HDRP/Decal และ Shader Graph แบบ Decal
    private static bool IsDecalMaterial(Material mat) =>
        mat.shader != null && (mat.FindPass("DBufferProjector") >= 0 || mat.shader.name.Contains("Decal"));

    // ขนาดด้านยาว = 1 ด้านสั้นคิดตามสัดส่วนรูป ใบตองรูปผืนผ้าจะได้ไม่ถูกบีบเป็นจัตุรัส
    private static Vector2 GetAspect(Material mat)
    {
        Texture tex = mat.HasProperty("_BaseColorMap") ? mat.GetTexture("_BaseColorMap") : mat.mainTexture;
        if (tex == null)
            return Vector2.one;

        int width = tex.width;
        int height = tex.height;

        // Unity อาจยืดรูปเป็นเลขยกกำลังสองตอน import — ใช้ขนาดไฟล์ต้นฉบับแทน
        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter importer)
            importer.GetSourceTextureWidthAndHeight(out width, out height);

        if (width <= 0 || height <= 0)
            return Vector2.one;

        return width >= height
            ? new Vector2(1f, (float)height / width)
            : new Vector2((float)width / height, 1f);
    }

    private static Transform FindContainer(bool create)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == ContainerName)
                return root.transform;
        }

        if (!create)
            return null;

        var go = new GameObject(ContainerName);
        Undo.RegisterCreatedObjectUndo(go, "Create Decals Container");
        return go.transform;
    }

    // ---------- จำค่าไว้ ปิดหน้าต่างแล้วเปิดใหม่ค่าไม่หาย ----------

    private void Save()
    {
        _s.Guids.Clear();
        _s.Enabled.Clear();
        foreach (Entry entry in _entries)
        {
            if (entry.Material == null)
                continue;
            _s.Guids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.Material)));
            _s.Enabled.Add(entry.Enabled);
        }
        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_s));
    }

    private void Load()
    {
        _entries.Clear();
        string json = EditorPrefs.GetString(PrefsKey, string.Empty);
        try
        {
            _s = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
        }
        catch (System.ArgumentException)
        {
            _s = null;
        }

        if (_s == null)
            _s = new SaveData();
        if (_s.Guids == null)
            _s.Guids = new List<string>();
        if (_s.Enabled == null)
            _s.Enabled = new List<bool>();

        for (int i = 0; i < _s.Guids.Count; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(_s.Guids[i]);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                _entries.Add(new Entry { Material = mat, Enabled = i < _s.Enabled.Count && _s.Enabled[i] });
        }
    }
}
