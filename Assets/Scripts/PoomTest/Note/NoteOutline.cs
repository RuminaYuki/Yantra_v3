using System.Collections.Generic;
using UnityEngine;
using Yantra.UI;

// กรอบขาวรอบโน้ต / กระดาษ — เดินเข้าใกล้แล้วขึ้นเส้นขาวคม ๆ รอบแผ่นกระดาษ
// วาดบนจอ (UI) ไม่ได้วาดในฉาก: เส้นหนาเท่ากันทุกระยะ ไม่โดนแสงหรือความมืดของฉาก
// วางบนตัวโน้ต (ตัวเดียวกับ Lever Interactor) — ใช้กับของแบน ๆ อย่างกระดาษ / ป้าย / รูปถ่าย
//
// วาดตอนท้ายสุดของเฟรม (Canvas.preWillRenderCanvases) หลังกล้องขยับเสร็จหมดแล้ว
// — กล้อง / Cinemachine / ส่ายหัว / กล้องสั่น มักขยับใน LateUpdate ถ้าวาดใน LateUpdate จะช้าไป 1 เฟรม กรอบจะหลุดจากกระดาษตอนหันกล้อง
public class NoteOutline : MonoBehaviour
{
    [Header("กระดาษ")]
    [Tooltip("โมเดลกระดาษ เช่น Nont — เว้นว่าง = หาเองจากลูก (ข้าม Highlight / Focus / _Glint)")]
    [SerializeField] private Renderer _paper;

    [Header("ขึ้นตอนไหน")]
    [Tooltip("ขึ้นกรอบเมื่อกล้องอยู่ใกล้กว่ากี่เมตร")]
    [SerializeField, Min(0.1f)] private float _showDistance = 3.5f;

    [Tooltip("ต้องหันไปทางโน้ตด้วยไหม — 0 = ไม่ต้อง / 25 = โน้ตห่างจากกลางจอไม่เกิน 25 องศา")]
    [SerializeField, Range(0f, 90f)] private float _lookAngle = 0f;

    [Tooltip("มีอะไรบังโน้ตอยู่ = ไม่ขึ้นกรอบ (กรอบวาดบนจอ ไม่งั้นจะเห็นทะลุกำแพง)")]
    [SerializeField] private bool _hideWhenBlocked = true;
    [SerializeField] private LayerMask _blockers = ~0;

    [Header("หน้าตา")]
    [SerializeField] private Color _color = Color.white;

    [Tooltip("ความหนาเส้น (พิกเซล ที่จอสูง 1080)")]
    [SerializeField, Min(0.5f)] private float _thickness = 2.5f;

    [Tooltip("แสงเรืองจาง ๆ รอบเส้น — 0 = ไม่มี")]
    [SerializeField, Range(0f, 1f)] private float _glow = 0.35f;
    [SerializeField, Min(0f)] private float _glowWidth = 8f;

    [Tooltip("กรอบใหญ่กว่ากระดาษกี่ส่วน (0.04 = 4%)")]
    [SerializeField, Range(0f, 0.3f)] private float _margin = 0.04f;

    [SerializeField, Min(0.1f)] private float _fadeSpeed = 6f;

    private bool _active = true;
    private float _alpha;
    private Mesh _paperMesh;
    private Camera _camera;
    private float _blockTimer;
    private bool _blocked;

    // ตัววาดกรอบ: mesh ชิ้นเดียว มุมต่อกันแบบตัดเฉียง ไม่มีปลายเส้นโผล่
    private RectTransform _rect;
    private CanvasRenderer _renderer;
    private Mesh _outlineMesh;
    private float _minEdge;

    private readonly Vector3[] _corners = new Vector3[4];
    private readonly Vector2[] _local = new Vector2[4];
    private readonly Vector2[] _normals = new Vector2[4];
    private readonly Vector2[] _miter = new Vector2[4];

    private static readonly List<Vector3> s_verts = new List<Vector3>(64);
    private static readonly List<Color32> s_colors = new List<Color32>(64);
    private static readonly List<Vector2> s_uvs = new List<Vector2>(64);
    private static readonly List<int> s_tris = new List<int>(192);
    private static Canvas _canvas;

    /// <summary>เปิด/ปิดกรอบ — ต่อกับ Interactor Event: On Interact = ปิด / On End Interact = เปิด</summary>
    public void SetActiveOutline(bool active) => _active = active;

    private void Awake()
    {
        if (_paper == null) _paper = FindPaper();

        if (_paper != null && _paper.TryGetComponent(out MeshFilter filter))
            _paperMesh = filter.sharedMesh;

        if (_paperMesh == null)
            Debug.LogWarning($"[NoteOutline] {name} หาโมเดลกระดาษไม่เจอ — ลากตัวกระดาษ (เช่น Nont) ใส่ช่อง Paper", this);
    }

    private void OnEnable()
    {
        Canvas.preWillRenderCanvases += DrawNow;
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= DrawNow;
        _alpha = 0f;
        Hide();
    }

    private void OnDestroy()
    {
        if (_rect != null) Destroy(_rect.gameObject);
        if (_outlineMesh != null) Destroy(_outlineMesh);
    }

    // เช็คว่าควรขึ้นไหม + จางเข้าออก (ตำแหน่งกรอบไปคำนวณตอนวาดทีเดียว)
    private void LateUpdate()
    {
        if (_paperMesh == null || _paper == null) return;
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        bool show = ShouldShow();
        _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, _fadeSpeed * Time.unscaledDeltaTime);
    }

    // ---------- ขึ้นไหม ----------

    private bool ShouldShow()
    {
        if (!_active || !_paper.enabled || !_paper.gameObject.activeInHierarchy) return false;

        var ui = UIManager.Instance;
        if (ui != null && ui.HasAnyOpen) return false;

        Vector3 eye = _camera.transform.position;
        Vector3 center = _paper.bounds.center;
        Vector3 toNote = center - eye;
        float distance = toNote.magnitude;
        if (distance > _showDistance) return false;

        if (_lookAngle > 0f && Vector3.Angle(_camera.transform.forward, toNote) > _lookAngle) return false;

        if (_hideWhenBlocked)
        {
            // ยิงเช็คกำแพงเป็นช่วง ๆ ไม่ต้องทุกเฟรม
            _blockTimer -= Time.unscaledDeltaTime;
            if (_blockTimer <= 0f)
            {
                _blockTimer = 0.15f;
                _blocked = IsBlocked(eye, toNote, distance);
            }
            if (_blocked) return false;
        }

        return true;
    }

    private bool IsBlocked(Vector3 eye, Vector3 toNote, float distance)
    {
        if (distance < 0.05f) return false;

        // หยุดก่อนถึงตัวกระดาษนิดนึง — ไม่ให้ชนโต๊ะที่กระดาษวางอยู่
        if (!Physics.Raycast(eye, toNote / distance, out RaycastHit hit, distance - 0.05f, _blockers, QueryTriggerInteraction.Ignore))
            return false;

        Transform t = hit.transform;
        if (t.IsChildOf(transform)) return false;                  // ตัวโน้ตเอง
        if (t.IsChildOf(_camera.transform.root)) return false;     // ตัวผู้เล่น
        return true;
    }

    // ---------- วาด ----------

    // เรียกทุกเฟรม ก่อน UI วาดขึ้นจอ — กล้องอยู่ตำแหน่งสุดท้ายของเฟรมแล้ว
    private void DrawNow()
    {
        if (_alpha <= 0.001f || _camera == null || _paper == null || _paperMesh == null)
        {
            Hide();
            return;
        }

        EnsureRenderer();
        float canvasScale = Mathf.Max(_canvas.scaleFactor, 0.0001f);

        if (!ProjectCorners(canvasScale) || !ComputeMiters())
        {
            Hide();
            return;
        }

        BuildMesh(canvasScale);
    }

    // มุม 4 มุมของกระดาษ: แกนที่บางที่สุดคือด้านหนา อีก 2 แกนคือด้านกว้าง/ยาว
    private bool ProjectCorners(float canvasScale)
    {
        Bounds b = _paperMesh.bounds;
        Vector3 e = b.extents * (1f + _margin);

        int thin = e.x <= e.y && e.x <= e.z ? 0 : (e.y <= e.z ? 1 : 2);
        int a = (thin + 1) % 3;
        int c = (thin + 2) % 3;

        Vector3 axisA = Vector3.zero; axisA[a] = e[a];
        Vector3 axisC = Vector3.zero; axisC[c] = e[c];

        _corners[0] = b.center - axisA - axisC;
        _corners[1] = b.center + axisA - axisC;
        _corners[2] = b.center + axisA + axisC;
        _corners[3] = b.center - axisA + axisC;

        Transform t = _paper.transform;
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = _camera.WorldToScreenPoint(t.TransformPoint(_corners[i]));
            if (p.z <= 0.01f) return false;   // อยู่ข้างหลังกล้อง
            _local[i] = new Vector2(p.x / canvasScale, p.y / canvasScale);
        }
        return true;
    }

    // ทิศดันออกของแต่ละมุม — มุมตัดเฉียงพอดี เส้นต่อกันสนิท ไม่มีปลายโผล่
    private bool ComputeMiters()
    {
        float area = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = _local[i];
            Vector2 q = _local[(i + 1) % 4];
            area += p.x * q.y - q.x * p.y;
        }
        if (Mathf.Abs(area) < 16f) return false;   // มองจากด้านข้างพอดี เห็นแค่สันกระดาษ

        float side = area > 0f ? 1f : -1f;
        _minEdge = float.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            Vector2 d = _local[(i + 1) % 4] - _local[i];
            float length = d.magnitude;
            if (length < 0.001f) return false;
            _minEdge = Mathf.Min(_minEdge, length);
            d /= length;
            _normals[i] = new Vector2(d.y, -d.x) * side;   // ชี้ออกนอกกรอบ
        }

        for (int i = 0; i < 4; i++)
        {
            Vector2 n1 = _normals[(i + 3) % 4];
            Vector2 n2 = _normals[i];
            Vector2 m = n1 + n2;
            m = m.sqrMagnitude < 0.000001f ? n2 : m.normalized;
            // มุมแหลมมาก ๆ (มองกระดาษเฉียงจัด) — จำกัดไว้ไม่ให้มุมแหลมพุ่งยาว
            float k = 1f / Mathf.Max(Vector2.Dot(m, n2), 0.35f);
            _miter[i] = m * k;
        }
        return true;
    }

    private void BuildMesh(float canvasScale)
    {
        float px = Screen.height / 1080f / canvasScale;   // 1 พิกเซลที่จอ 1080 ในหน่วยของ canvas
        float half = _thickness * px * 0.5f;
        float aa = 1f / canvasScale;                       // ขอบเนียน 1 พิกเซลจริง
        float glow = _glowWidth * px;
        float innerGlow = Mathf.Min(glow * 0.5f, _minEdge * 0.25f);   // ด้านในไม่ล้นเข้าไปกลางกระดาษตอนโน้ตอยู่ไกล

        Color32 line = Tint(_alpha);
        Color32 clear = Tint(0f);
        Color32 glowStrong = Tint(_alpha * _glow);
        Color32 glowSoft = Tint(_alpha * _glow * 0.3f);

        s_verts.Clear();
        s_colors.Clear();
        s_uvs.Clear();
        s_tris.Clear();

        // แสงเรืองก่อน เส้นทับ
        if (_glow > 0f && glow > 0f)
        {
            float mid = half + glow * 0.35f;
            Ring(half, mid, glowStrong, glowSoft);
            Ring(mid, half + glow, glowSoft, clear);
            if (innerGlow > 0f) Ring(-half - innerGlow, -half, clear, glowStrong);
        }

        Ring(-half - aa, -half, clear, line);
        Ring(-half, half, line, line);
        Ring(half, half + aa, line, clear);

        _outlineMesh.Clear();
        _outlineMesh.SetVertices(s_verts);
        _outlineMesh.SetColors(s_colors);
        _outlineMesh.SetUVs(0, s_uvs);
        _outlineMesh.SetTriangles(s_tris, 0);

        _renderer.SetMesh(_outlineMesh);
        _renderer.cull = false;
    }

    // แถบรอบกรอบ 1 วง จากระยะ inner ถึง outer (บวก = ออกนอก / ลบ = เข้าใน)
    private void Ring(float inner, float outer, Color32 innerColor, Color32 outerColor)
    {
        int start = s_verts.Count;
        for (int i = 0; i < 4; i++)
        {
            s_verts.Add(_local[i] + _miter[i] * inner);
            s_verts.Add(_local[i] + _miter[i] * outer);
            s_colors.Add(innerColor);
            s_colors.Add(outerColor);
            s_uvs.Add(Vector2.zero);
            s_uvs.Add(Vector2.zero);
        }

        for (int i = 0; i < 4; i++)
        {
            int a = start + i * 2;
            int b = start + (i + 1) % 4 * 2;
            s_tris.Add(a); s_tris.Add(a + 1); s_tris.Add(b + 1);
            s_tris.Add(a); s_tris.Add(b + 1); s_tris.Add(b);
        }
    }

    private Color32 Tint(float alpha)
    {
        Color c = _color;
        c.a *= Mathf.Clamp01(alpha);
        return c;
    }

    private void Hide()
    {
        if (_renderer != null) _renderer.cull = true;
    }

    // ---------- สร้างของเอง ----------

    private void EnsureRenderer()
    {
        if (_outlineMesh == null)
        {
            _outlineMesh = new Mesh { name = "NoteOutline" };
            _outlineMesh.MarkDynamic();
        }

        if (_renderer != null) return;

        Canvas canvas = SharedCanvas();
        var go = new GameObject($"{name}_Outline", typeof(RectTransform), typeof(CanvasRenderer));
        _rect = (RectTransform)go.transform;
        _rect.SetParent(canvas.transform, false);
        _rect.pivot = Vector2.zero;                // จุด 0,0 = มุมซ้ายล่างของจอ ตรงกับพิกัดกล้อง
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;

        _renderer = go.GetComponent<CanvasRenderer>();
        _renderer.materialCount = 1;
        _renderer.SetMaterial(Canvas.GetDefaultCanvasMaterial(), 0);
        _renderer.SetTexture(Texture2D.whiteTexture);
        _renderer.cull = true;
    }

    // Canvas เดียวใช้ร่วมกันทุกโน้ต — อยู่ใต้ HUD / มือ / เมนู
    private static Canvas SharedCanvas()
    {
        if (_canvas != null) return _canvas;

        var go = new GameObject("NoteOutlines", typeof(RectTransform), typeof(Canvas));
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = -5;
        return _canvas;
    }

    private Renderer FindPaper()
    {
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>())
        {
            if (r.name.EndsWith("_Glint")) continue;
            if (UnderHelper(r.transform)) continue;
            if (r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null) return r;
        }
        return null;
    }

    // ข้ามแผ่น Highlight / Focus ของระบบ Interact
    private bool UnderHelper(Transform t)
    {
        for (; t != null && t != transform; t = t.parent)
            if (t.name == "Highlight" || t.name == "Focus") return true;
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Renderer paper = _paper != null ? _paper : FindPaper();
        if (paper == null) return;
        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(paper.bounds.center, _showDistance);
    }
#endif
}