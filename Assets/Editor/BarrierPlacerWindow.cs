using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

// เครื่องมือวาง Barrier — คลิกในฉากทีละจุด ระหว่างสองจุดได้กำแพง 1 ท่อน
// ยืดความยาว / หมุน / แนบพื้นต่างระดับให้เอง เป็น Prefab ตัวเดิม (ยังลิงก์กับ Prefab อยู่) กด Ctrl+Z ได้ทุกท่อน
// เปิดจาก Tools > Yantra > Barrier Placer
public class BarrierPlacerWindow : EditorWindow
{
    private const string Prefs = "Yantra.BarrierPlacer.";

    [SerializeField] private GameObject _prefab;
    [SerializeField] private Transform _parent;
    [SerializeField] private float _height = 9f;
    [SerializeField] private float _thickness = 1.8f;
    [SerializeField] private float _sink = 1f;
    [SerializeField] private bool _followGround = true;
    [SerializeField] private bool _extendCorners = true;
    [SerializeField] private float _endSnap = 1.5f;
    [SerializeField] private float _angleSnap = 15f;
    [SerializeField] private int _groundMask = ~0;

    private bool _active;
    private bool _toolsWereHidden;

    // เส้นที่กำลังวาง — จุดทั้งหมด กับกำแพงที่สร้างไปแล้ว (จำนวน = จุด - 1)
    private readonly List<Vector3> _points = new();
    private readonly List<GameObject> _created = new();

    // ปลายกำแพงที่มีอยู่ในฉาก — ไว้ดูดให้ต่อกันสนิท
    private readonly List<Vector3> _ends = new();

    private readonly RaycastHit[] _hits = new RaycastHit[32];

    private Vector2 _mousePos;
    private Vector3 _hover;
    private bool _hoverValid;
    private bool _hoverSnapped;
    private bool _hoverClosesLoop;

    [MenuItem("Tools/Yantra/Barrier Placer")]
    private static void Open() => GetWindow<BarrierPlacerWindow>("Barrier Placer");

    private void OnEnable()
    {
        LoadPrefs();
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        SetActive(false);
        SavePrefs();
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    // วางตอน Play แล้วของหายตอนหยุด — ปิดเครื่องมือไว้ก่อน
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) SetActive(false);
    }

    private void OnSelectionChange() => Repaint();

    // ---------- หน้าต่าง ----------

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("กำแพง", EditorStyles.boldLabel);
        _prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prefab", "เช่น BarrierBlock"), _prefab, typeof(GameObject), false);
        _parent = (Transform)EditorGUILayout.ObjectField(new GUIContent("ใส่ไว้ใต้", "Object ที่จะเก็บกำแพงทั้งหมดไว้ข้างใต้\nเว้นว่าง = วางที่ root ของฉาก"), _parent, typeof(Transform), true);
        _height = EditorGUILayout.FloatField(new GUIContent("สูงจากพื้น (ม.)"), _height);
        _thickness = EditorGUILayout.FloatField(new GUIContent("หนา (ม.)"), _thickness);
        _sink = EditorGUILayout.FloatField(new GUIContent("จมลงดิน (ม.)", "กันพื้นต่างระดับแล้วเห็นช่องใต้กำแพง"), _sink);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("พื้น", EditorStyles.boldLabel);
        _followGround = EditorGUILayout.Toggle(new GUIContent("ตามพื้นต่างระดับ", "ยิงหาพื้นตลอดแนวกำแพง ให้ใต้กำแพงไม่ลอยตรงที่พื้นต่ำ\nวางในอาคารที่มีเพดานให้ปิด"), _followGround);
        _groundMask = LayerMaskField(new GUIContent("Layer ของพื้น", "คลิกแล้วโดนอะไรบ้าง — ไม่ต้องเอา Layer ของกำแพงมาด้วย"), _groundMask);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ตัวช่วย", EditorStyles.boldLabel);
        _extendCorners = EditorGUILayout.Toggle(new GUIContent("ปิดมุมให้สนิท", "ยืดปลายกำแพงออกครึ่งความหนา มุมที่ต่อกันจะไม่มีร่อง"), _extendCorners);
        _endSnap = EditorGUILayout.FloatField(new GUIContent("ดูดเข้าปลายกำแพง (ม.)", "เมาส์ใกล้ปลายกำแพงที่มีอยู่แค่นี้ จุดจะดูดเข้าไปต่อให้สนิท — 0 = ไม่ดูด"), _endSnap);
        _angleSnap = EditorGUILayout.FloatField(new GUIContent("ล็อกมุมทีละ (องศา)", "กด Shift ค้างตอนวาง แนวกำแพงจะล็อกเป็นมุมทีละเท่านี้"), _angleSnap);

        if (EditorGUI.EndChangeCheck())
        {
            _height = Mathf.Max(0.1f, _height);
            _thickness = Mathf.Max(0.05f, _thickness);
            _sink = Mathf.Max(0f, _sink);
            _endSnap = Mathf.Max(0f, _endSnap);
            _angleSnap = Mathf.Clamp(_angleSnap, 0f, 90f);
            SavePrefs();
            RefreshEnds();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(8);

        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = _active ? new Color(1f, 0.55f, 0.5f) : new Color(0.55f, 1f, 0.6f);
        if (GUILayout.Button(_active ? "หยุดวาง" : "เริ่มวาง", GUILayout.Height(32)))
            SetActive(!_active);
        GUI.backgroundColor = oldColor;

        if (_active)
        {
            EditorGUILayout.HelpBox(
                "คลิกซ้ายในฉาก = วางจุด (ตั้งแต่จุดที่ 2 ได้กำแพงทันที)\n" +
                "Shift ค้าง = ล็อกมุม  |  Backspace = ลบท่อนล่าสุด\n" +
                "Enter / Esc = จบเส้นนี้  |  Esc อีกที = หยุดวาง\n" +
                "คลิกจุดแรกของเส้น = ปิดเป็นวง",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _points.Count > 0;
                if (GUILayout.Button("จบเส้นนี้")) EndChain();
                GUI.enabled = _created.Count > 0;
                if (GUILayout.Button("ลบท่อนล่าสุด")) RemoveLast();
                GUI.enabled = true;
            }
        }

        if (_prefab == null)
            EditorGUILayout.HelpBox("ยังไม่ได้ใส่ Prefab — จะวางเป็น Cube เปล่าแทน", MessageType.Warning);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("กำแพงที่วางไว้แล้ว", EditorStyles.boldLabel);
        GUI.enabled = Selection.gameObjects.Length > 0;
        if (GUILayout.Button(new GUIContent("แนบพื้นใหม่ (ตัวที่เลือกอยู่)", "ใช้ความสูง / จมดิน ตามค่าข้างบน — ความยาว ความหนา และแนวเดิม")))
            RefitSelected();
        GUI.enabled = true;
    }

    private static int LayerMaskField(GUIContent label, int mask)
    {
        int concatenated = InternalEditorUtility.LayerMaskToConcatenatedLayersMask(mask);
        concatenated = EditorGUILayout.MaskField(label, concatenated, InternalEditorUtility.layers);
        return InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(concatenated);
    }

    private void SetActive(bool on)
    {
        if (_active == on) return;
        _active = on;

        // ซ่อนลูกศรย้ายของ Object ที่เลือกไว้ ไม่ให้แย่งคลิก
        if (on)
        {
            _toolsWereHidden = Tools.hidden;
            Tools.hidden = true;
            RefreshEnds();
        }
        else
        {
            Tools.hidden = _toolsWereHidden;
            EndChain();
        }

        SceneView.RepaintAll();
        Repaint();
    }

    // ---------- ในฉาก ----------

    private void OnSceneGUI(SceneView view)
    {
        if (!_active) return;

        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);

        switch (e.type)
        {
            case EventType.Layout:
                HandleUtility.AddDefaultControl(id);   // คลิกแล้วไม่ไปเลือก Object อื่น
                break;

            case EventType.MouseMove:
                UpdateHover(e.mousePosition, e.shift);
                view.Repaint();
                break;

            case EventType.MouseDown:
                if (e.button != 0 || e.alt) break;   // Alt + คลิก = หมุนกล้อง
                UpdateHover(e.mousePosition, e.shift);
                if (_hoverValid) Place();
                e.Use();
                break;

            case EventType.KeyDown:
                HandleKeyDown(e);
                break;

            case EventType.KeyUp:
                if (e.keyCode != KeyCode.LeftShift && e.keyCode != KeyCode.RightShift) break;
                UpdateHover(_mousePos, false);
                view.Repaint();
                break;

            case EventType.Repaint:
                Draw();
                DrawOverlay();
                break;
        }
    }

    private void HandleKeyDown(Event e)
    {
        switch (e.keyCode)
        {
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                EndChain();
                e.Use();
                break;

            case KeyCode.Escape:
                if (_points.Count > 0) EndChain();
                else SetActive(false);
                e.Use();
                break;

            case KeyCode.Backspace:
                RemoveLast();
                e.Use();
                break;

            case KeyCode.LeftShift:
            case KeyCode.RightShift:
                UpdateHover(_mousePos, true);
                SceneView.RepaintAll();
                break;
        }
    }

    private void UpdateHover(Vector2 mouse, bool shift)
    {
        _mousePos = mouse;
        _hoverSnapped = false;
        _hoverClosesLoop = false;

        Physics.SyncTransforms();
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        _hoverValid = RaycastGround(ray, out _hover);
        if (!_hoverValid) return;

        // ดูดเข้าปลายกำแพงก่อน ไม่เจอค่อยล็อกมุม
        if (TrySnapToEnd()) return;

        if (shift && _points.Count > 0 && _angleSnap > 0.01f)
            SnapAngle();
    }

    private bool RaycastGround(Ray ray, out Vector3 point)
    {
        if (CastGround(ray, 10000f, out RaycastHit hit))
        {
            point = hit.point;
            return true;
        }

        // ไม่โดนอะไรเลย (เช่นมองฟ้า) — ใช้ระนาบแนวนอนที่ความสูงของจุดล่าสุดแทน
        float y = _points.Count > 0 ? _points[^1].y : 0f;
        var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = default;
        return false;
    }

    private bool TrySnapToEnd()
    {
        if (_endSnap <= 0f) return false;

        float best = _endSnap * _endSnap;
        bool found = false;
        bool closes = false;
        Vector3 target = default;
        bool hasLast = _points.Count > 0;
        Vector3 last = hasLast ? _points[^1] : default;

        foreach (Vector3 end in _ends)
        {
            if (hasLast && SqrDistanceXZ(end, last) < 0.01f) continue;   // ปลายของท่อนที่เพิ่งวาง = จุดเดิม
            float d = SqrDistanceXZ(end, _hover);
            if (d >= best) continue;
            best = d;
            target = end;
            found = true;
            closes = false;
        }

        // จุดแรกของเส้นนี้ — คลิกแล้วปิดเป็นวง
        if (_points.Count >= 3)
        {
            float d = SqrDistanceXZ(_points[0], _hover);
            if (d < best)
            {
                target = _points[0];
                found = true;
                closes = true;
            }
        }

        if (!found) return false;

        if (!closes && GroundAt(target, _hover.y + 50f, out float y)) target.y = y;
        else if (!closes) target.y = _hover.y;

        _hover = target;
        _hoverSnapped = true;
        _hoverClosesLoop = closes;
        return true;
    }

    private void SnapAngle()
    {
        Vector3 last = _points[^1];
        Vector3 flat = _hover - last;
        flat.y = 0f;
        float length = flat.magnitude;
        if (length < 0.01f) return;

        float angle = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        angle = Mathf.Round(angle / _angleSnap) * _angleSnap;

        Vector3 snapped = last + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * length;
        snapped.y = GroundAt(snapped, Mathf.Max(last.y, _hover.y) + 50f, out float y) ? y : _hover.y;
        _hover = snapped;
    }

    // ---------- วาง ----------

    private void Place()
    {
        Vector3 point = _hover;
        bool closes = _hoverClosesLoop;

        if (_points.Count > 0)
        {
            GameObject wall = CreateSegment(_points[^1], point);
            if (wall == null) return;   // สั้นเกินไป
            _created.Add(wall);
        }

        _points.Add(point);
        RefreshEnds();

        if (closes) EndChain();
        Repaint();
    }

    private GameObject CreateSegment(Vector3 a, Vector3 b)
    {
        if (!ComputeSegment(a, b, _thickness, out Vector3 pos, out Quaternion rot, out Vector3 scale))
            return null;

        GameObject wall;
        if (_prefab != null)
        {
            wall = (GameObject)PrefabUtility.InstantiatePrefab(_prefab, _parent);
        }
        else
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(_parent, false);
        }

        wall.name = GameObjectUtility.GetUniqueNameForSibling(_parent, _prefab != null ? _prefab.name : "Barrier");
        Undo.RegisterCreatedObjectUndo(wall, "วาง Barrier");
        ApplyTransform(wall.transform, pos, rot, scale);
        return wall;
    }

    // กล่องกำแพงจากจุด a ไป b — Cube ขนาด 1: X = หนา, Y = สูง, Z = ยาวตามแนว
    private bool ComputeSegment(Vector3 a, Vector3 b, float thickness, out Vector3 pos, out Quaternion rot, out Vector3 scale)
    {
        Vector3 flat = b - a;
        flat.y = 0f;
        float length = flat.magnitude;

        if (length < 0.1f)
        {
            pos = default;
            rot = Quaternion.identity;
            scale = default;
            return false;
        }

        Vector3 dir = flat / length;
        float extend = _extendCorners ? thickness * 0.5f : 0f;
        Vector3 start = a - dir * extend;
        Vector3 end = b + dir * extend;

        // หลังคากำแพงเรียบ สูงจากปลายที่สูงกว่า / ใต้กำแพงลงไปถึงพื้นที่ต่ำสุดตลอดแนว
        float bottom = Mathf.Min(a.y, b.y);
        float top = Mathf.Max(a.y, b.y) + _height;

        if (_followGround)
        {
            float fromY = Mathf.Max(a.y, b.y) + 50f;
            int steps = Mathf.Clamp(Mathf.CeilToInt((length + extend * 2f) / 1.5f), 1, 200);
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(start, end, (float)i / steps);
                if (GroundAt(p, fromY, out float y)) bottom = Mathf.Min(bottom, y);
            }
        }

        bottom -= _sink;

        pos = (start + end) * 0.5f;
        pos.y = (bottom + top) * 0.5f;
        rot = Quaternion.LookRotation(dir, Vector3.up);
        scale = new Vector3(thickness, top - bottom, length + extend * 2f);
        return true;
    }

    private static void ApplyTransform(Transform t, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        t.SetPositionAndRotation(pos, rot);
        Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
    }

    private void RemoveLast()
    {
        if (_created.Count > 0)
        {
            GameObject wall = _created[^1];
            _created.RemoveAt(_created.Count - 1);
            _points.RemoveAt(_points.Count - 1);
            if (wall != null) Undo.DestroyObjectImmediate(wall);
        }
        else
        {
            _points.Clear();
        }

        RefreshEnds();
        SceneView.RepaintAll();
        Repaint();
    }

    private void EndChain()
    {
        _points.Clear();
        _created.Clear();
        RefreshEnds();
        SceneView.RepaintAll();
        Repaint();
    }

    // กด Ctrl+Z ไปแล้ว — ถอยจุดตามท่อนที่หายไป
    private void OnUndoRedo()
    {
        while (_created.Count > 0 && _created[^1] == null)
        {
            _created.RemoveAt(_created.Count - 1);
            _points.RemoveAt(_points.Count - 1);
        }

        RefreshEnds();
        SceneView.RepaintAll();
        Repaint();
    }

    // ---------- แนบพื้นใหม่ ----------

    private void RefitSelected()
    {
        Physics.SyncTransforms();
        int count = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            Transform t = go.transform;
            Vector3 s = t.lossyScale;
            Vector3 forward = t.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) continue;
            forward.Normalize();

            float extend = _extendCorners ? s.x * 0.5f : 0f;
            float half = s.z * 0.5f - extend;
            if (half <= 0.05f) continue;

            Vector3 a = t.position - forward * half;
            Vector3 b = t.position + forward * half;
            float fromY = t.position.y + s.y * 0.5f + 50f;
            float fallback = t.position.y - s.y * 0.5f + _sink;
            a.y = GroundAt(a, fromY, out float ya) ? ya : fallback;
            b.y = GroundAt(b, fromY, out float yb) ? yb : fallback;

            if (!ComputeSegment(a, b, s.x, out Vector3 pos, out Quaternion rot, out Vector3 scale)) continue;

            Undo.RecordObject(t, "แนบพื้น Barrier");
            ApplyTransform(t, pos, rot, scale);
            count++;
        }

        Debug.Log($"[BarrierPlacer] แนบพื้นใหม่ {count} ตัว");
        RefreshEnds();
    }

    // ---------- พื้น ----------

    private bool GroundAt(Vector3 p, float fromY, out float y)
    {
        var ray = new Ray(new Vector3(p.x, fromY, p.z), Vector3.down);
        if (CastGround(ray, 1000f, out RaycastHit hit))
        {
            y = hit.point.y;
            return true;
        }

        y = p.y;
        return false;
    }

    // ตัวที่ใกล้สุด โดยข้ามกำแพงที่วางไว้แล้ว — จะได้คลิกทะลุกำแพงลงพื้นได้
    private bool CastGround(Ray ray, float distance, out RaycastHit best)
    {
        int count = Physics.RaycastNonAlloc(ray, _hits, distance, _groundMask, QueryTriggerInteraction.Ignore);
        best = default;
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            if (_hits[i].distance >= bestDistance || IsBarrier(_hits[i].collider)) continue;
            best = _hits[i];
            bestDistance = best.distance;
            found = true;
        }

        return found;
    }

    private bool IsBarrier(Collider c)
    {
        if (_prefab == null || c == null) return false;
        return PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject) == _prefab;
    }

    private void RefreshEnds()
    {
        _ends.Clear();
        if (_prefab == null) return;

        GameObject[] instances;
        try
        {
            instances = PrefabUtility.FindAllInstancesOfPrefab(_prefab);
        }
        catch (System.ArgumentException)
        {
            return;   // ใส่ไฟล์ที่ไม่ใช่ Prefab (เช่น FBX) — แค่ไม่มีดูดปลาย
        }

        foreach (GameObject go in instances)
        {
            if (!go.activeInHierarchy) continue;

            Transform t = go.transform;
            Vector3 s = t.lossyScale;
            Vector3 forward = t.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) continue;
            forward.Normalize();

            // ปลายจริงลบส่วนที่ยืดปิดมุมออก — ต่อแล้วมุมพอดี
            float half = s.z * 0.5f - s.x * 0.5f;
            if (half <= 0f) continue;

            _ends.Add(t.position + forward * half);
            _ends.Add(t.position - forward * half);
        }
    }

    private static float SqrDistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    // ---------- วาดในฉาก ----------

    private void Draw()
    {
        // วาดทับทุกอย่าง มองเห็นแม้อยู่หลังภูเขา
        var oldZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        DrawPreview();
        Handles.zTest = oldZTest;
    }

    private void DrawPreview()
    {
        // ปลายกำแพงที่ดูดได้ ใกล้ ๆ เมาส์
        if (_hoverValid && _endSnap > 0f)
        {
            Handles.color = new Color(0.3f, 1f, 0.45f, 0.5f);
            foreach (Vector3 end in _ends)
            {
                if (SqrDistanceXZ(end, _hover) > 900f) continue;
                Vector3 p = new(end.x, _hover.y, end.z);
                Handles.DrawWireDisc(p, Vector3.up, _endSnap);
            }
        }

        // เส้นที่วางไปแล้ว
        if (_points.Count > 0)
        {
            Handles.color = new Color(0.3f, 0.85f, 1f);
            if (_points.Count > 1) Handles.DrawAAPolyLine(4f, _points.ToArray());
            foreach (Vector3 p in _points)
                Handles.SphereHandleCap(0, p, Quaternion.identity, HandleUtility.GetHandleSize(p) * 0.12f, EventType.Repaint);
        }

        if (!_hoverValid) return;

        float size = HandleUtility.GetHandleSize(_hover) * 0.15f;
        Handles.color = _hoverSnapped ? new Color(0.3f, 1f, 0.45f) : Color.white;
        Handles.DrawWireDisc(_hover, Vector3.up, _hoverSnapped ? size * 2f : size);

        if (_points.Count == 0) return;

        Vector3 last = _points[^1];
        if (!ComputeSegment(last, _hover, _thickness, out Vector3 pos, out Quaternion rot, out Vector3 scale)) return;

        // กล่องตัวอย่างของท่อนถัดไป
        using (new Handles.DrawingScope(new Color(1f, 0.8f, 0.2f), Matrix4x4.TRS(pos, rot, scale)))
            Handles.DrawWireCube(Vector3.zero, Vector3.one);

        Handles.color = new Color(1f, 0.8f, 0.2f);
        Handles.DrawAAPolyLine(3f, last, _hover);

        Vector3 flat = _hover - last;
        flat.y = 0f;
        float angle = Mathf.Repeat(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 360f);
        string text = _hoverClosesLoop ? "ปิดวง" : $"{flat.magnitude:0.0} ม.  |  {angle:0}°";
        Handles.Label(pos + Vector3.up * (scale.y * 0.5f + 0.5f), text, EditorStyles.whiteBoldLabel);
    }

    private void DrawOverlay()
    {
        Handles.BeginGUI();
        GUI.Label(new Rect(10f, 10f, 300f, 58f),
            "Barrier Placer — คลิก = วางจุด\nShift = ล็อกมุม  |  Backspace = ลบท่อนล่าสุด\nEnter / Esc = จบเส้น",
            EditorStyles.helpBox);
        Handles.EndGUI();
    }

    // ---------- จำค่าที่ตั้งไว้ ----------

    private void LoadPrefs()
    {
        if (_prefab == null)
        {
            string path = EditorPrefs.GetString(Prefs + "Prefab", "");
            if (!string.IsNullOrEmpty(path)) _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        if (_prefab == null) _prefab = FindDefaultPrefab();

        _height = EditorPrefs.GetFloat(Prefs + "Height", _height);
        _thickness = EditorPrefs.GetFloat(Prefs + "Thickness", _thickness);
        _sink = EditorPrefs.GetFloat(Prefs + "Sink", _sink);
        _followGround = EditorPrefs.GetBool(Prefs + "FollowGround", _followGround);
        _extendCorners = EditorPrefs.GetBool(Prefs + "ExtendCorners", _extendCorners);
        _endSnap = EditorPrefs.GetFloat(Prefs + "EndSnap", _endSnap);
        _angleSnap = EditorPrefs.GetFloat(Prefs + "AngleSnap", _angleSnap);
        _groundMask = EditorPrefs.GetInt(Prefs + "GroundMask", ~LayerMask.GetMask("Obstacle", "Ignore Raycast", "Player"));
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(Prefs + "Prefab", _prefab != null ? AssetDatabase.GetAssetPath(_prefab) : "");
        EditorPrefs.SetFloat(Prefs + "Height", _height);
        EditorPrefs.SetFloat(Prefs + "Thickness", _thickness);
        EditorPrefs.SetFloat(Prefs + "Sink", _sink);
        EditorPrefs.SetBool(Prefs + "FollowGround", _followGround);
        EditorPrefs.SetBool(Prefs + "ExtendCorners", _extendCorners);
        EditorPrefs.SetFloat(Prefs + "EndSnap", _endSnap);
        EditorPrefs.SetFloat(Prefs + "AngleSnap", _angleSnap);
        EditorPrefs.SetInt(Prefs + "GroundMask", _groundMask);
    }

    private static GameObject FindDefaultPrefab()
    {
        foreach (string guid in AssetDatabase.FindAssets("BarrierBlock t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == "BarrierBlock")
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }
}
