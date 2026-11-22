using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// เส้นเนื้อยืด ๆ บนก้อนเนื้อที่กระสือควักออกจากคอ
///
/// ตอนเกิด: เส้นขึงจากจุดยึด (ตอคอกระสือ / จุดที่ projectile เกิด) มาถึงก้อนเนื้อ
///          ก้อนยิ่งบินห่าง เส้นยิ่งยืดและบางลง พอเกินระยะก็ขาดทีละเส้น แล้วดีดกลับมาห้อยกับก้อน
/// ตอนบิน:  เส้นห้อยลากตามหลังแบบเชือก มีดิ้นนิด ๆ ให้ดูเป็นเนื้อสด
///
/// แปะบนตัวแม่ของ projectile (ตัวที่บิน) — ไม่ต้องแก้สคริปต์ยิง
/// เส้นถูกสร้างเป็น LineRenderer ลูกตอนเล่น ใช้กับ Object Pool ได้ (รีเซ็ตเองทุกครั้งที่เปิด)
/// เป็นของตกแต่งล้วน ไม่มีผลกับการชน / ดาเมจ
/// </summary>
[DisallowMultipleComponent]
public class FleshStrands : MonoBehaviour
{
    public enum TetherMode
    {
        [Tooltip("ไม่ขึงจากปาก — ห้อยอย่างเดียว")]
        None,

        [Tooltip("ขึงจากจุดที่ projectile เกิด (ปากกระสือ) — ไม่ต้องตั้งอะไรเพิ่ม")]
        SpawnPoint,

        [Tooltip("ขึงจาก Transform ในช่อง Anchor — ต้องมีสคริปต์มาสั่ง SetAnchor หรือใส่เองในฉาก")]
        Transform,

        [Tooltip("ขึงจาก Flesh Strand Anchor ที่ใกล้จุดเกิดที่สุด (เช่นตอคอกระสือ)\nไม่ต้องแก้สคริปต์ยิง — ไม่เจอจะขึงจากจุดเกิดแทน")]
        NearestAnchor,
    }

    [Header("เส้น")]
    [SerializeField, Range(1, 12)] private int _strandCount = 5;

    [Tooltip("จุดต่อเส้น — มากขึ้นโค้งเนียนขึ้นแต่หนักขึ้นนิดหน่อย")]
    [SerializeField, Range(4, 24)] private int _pointsPerStrand = 10;

    [Tooltip("ความยาวตอนห้อย (เมตร) สุ่มในช่วงนี้ทีละเส้น")]
    [SerializeField] private Vector2 _lengthRange = new(0.2f, 0.5f);

    [Tooltip("โคนเส้นกระจายรอบกลางก้อนเนื้อในรัศมีนี้ (เมตร)\n" +
             "เปิด Gizmos แล้วให้ทรงกลมสีแดงพอดีผิวก้อนเนื้อ")]
    [SerializeField, Min(0f)] private float _rootRadius = 0.06f;

    [Tooltip("ตอนขึง: เลื่อนโคนเส้นจากกลางก้อนไปทางตอคอ (เมตร)\n" +
             "เส้นจะงอกจากปลายก้อนฝั่งที่หันหาคอ ไม่ใช่โผล่จากอากาศรอบกลางก้อน\n" +
             "ให้ราวครึ่งหนึ่งของความยาวก้อน แต่ไม่เลยผิว (ก้อนยาว ~1 ม. ใส่ 0.3–0.4)")]
    [SerializeField, Min(0f)] private float _rootTowardAnchor = 0f;

    [Tooltip("วางโคนเส้นเองด้วย Transform ลูก (ไม่ใส่ก็ได้ — สุ่มรอบตัวให้)")]
    [SerializeField] private Transform[] _roots;

    [Header("ขึงจากตัวกระสือ")]
    [SerializeField] private TetherMode _tether = TetherMode.SpawnPoint;

    [Tooltip("ใช้เมื่อ Tether = Transform")]
    [SerializeField] private Transform _anchor;

    [Tooltip("ใช้เมื่อ Tether = Nearest Anchor — หาจุดยึดในระยะนี้จากจุดเกิด (เมตร)")]
    [SerializeField, Min(0.1f)] private float _anchorSearchRadius = 3f;

    [Tooltip("ปลายฝั่งปากกระจายในรัศมีนี้ (เมตร)")]
    [SerializeField, Min(0f)] private float _anchorRadius = 0.04f;

    [Tooltip("เส้นขาดเมื่อก้อนเนื้อห่างจากปากเท่านี้ (เมตร)\nสุ่มทีละเส้น เส้นจะได้ขาดไม่พร้อมกัน")]
    [SerializeField] private Vector2 _snapDistance = new(0.6f, 1.6f);

    [Tooltip("ความหย่อนตอนขึง — 0 = ตึงเปรี๊ยะ")]
    [SerializeField, Range(0f, 0.5f)] private float _sag = 0.08f;

    [Tooltip("เวลาดีดกลับหลังขาด (วินาที)\nต่ำกว่า 0.25 เส้นจะสะบัดเลยไปข้างหน้าก้อนเนื้อ ดูผิดธรรมชาติ")]
    [SerializeField, Min(0.05f)] private float _recoilTime = 0.3f;

    [Tooltip("ยิงตอนเส้นขาด พร้อมตำแหน่งที่ขาด — เอาไปเกิดเลือดหยด / VFX ได้")]
    [SerializeField] private UnityEvent<Vector3> _onSnap;

    [Header("ฟิสิกส์")]
    [SerializeField] private float _gravityScale = 1f;

    [Tooltip("แรงต้านอากาศ — มาก = เส้นลากตามหลังมากและหยุดแกว่งไว")]
    [SerializeField, Range(0f, 0.5f)] private float _damping = 0.06f;

    [Tooltip("ดิ้นแบบเนื้อสด (0 = นิ่ง)")]
    [SerializeField, Min(0f)] private float _wiggle = 1.5f;

    [SerializeField, Min(0f)] private float _wiggleSpeed = 3f;

    [Tooltip("รอบคำนวณความยาวเส้นต่อเฟรม — มาก = เส้นยืดน้อยลง")]
    [SerializeField, Range(1, 12)] private int _iterations = 6;

    [Header("หน้าตา")]
    [SerializeField] private Material _material;

    [Tooltip("ความหนาที่โคน (เมตร)")]
    [SerializeField, Min(0.001f)] private float _rootWidth = 0.02f;

    [Tooltip("ความหนาที่ปลายตอนห้อย เทียบกับโคน")]
    [SerializeField, Range(0.05f, 1f)] private float _tipWidth = 0.25f;

    [Tooltip("ยืดแล้วบางลงได้สุดแค่ไหน เทียบกับตอนปกติ")]
    [SerializeField, Range(0.05f, 1f)] private float _thinnestWhenStretched = 0.3f;

    [SerializeField] private bool _castShadows = false;

    private class Strand
    {
        public LineRenderer Line;
        public Vector3[] Pos;
        public Vector3[] Prev;
        public Transform Root;          // ถ้าวางโคนเอง
        public Vector3 RootLocal;       // ถ้าสุ่มให้
        public Vector3 FreeRootLocal;   // โคนหลังขาด — ติดอยู่ที่เดิมบนก้อน
        public Vector3 AnchorOffset;
        public float FreeLength;
        public float SnapAt;
        public float Seed;
        public bool Tethered;
        public float Recoil;            // 1 → 0 หลังขาด
        public float StretchedLength;   // ความยาวตอนขาด
    }

    private Strand[] _strands;
    private AnimationCurve _freeCurve;
    private AnimationCurve _tetherCurve;
    private Vector3 _spawnPoint;
    private FleshStrandAnchor _foundAnchor;   // ที่หาเจอตอนเกิด (Nearest Anchor)
    private Vector3 _lastAnchorPoint;    // จุดยึดหาย (เช่นกระสือตาย) เส้นค้างที่เดิมแทน
    private bool _needsReset;
    private float _prevDt;

    /// <summary>ให้สคริปต์ยิงสั่งขึงจากกระดูกปากได้ ถ้าอยากให้ตามหัวที่ขยับ</summary>
    public void SetAnchor(Transform anchor)
    {
        _anchor = anchor;
        _tether = anchor != null ? TetherMode.Transform : TetherMode.SpawnPoint;
    }

    private void Awake()
    {
        if (_material == null)
            Debug.LogWarning($"[FleshStrands] {name} ยังไม่ได้ใส่ Material — เส้นจะเป็นสีชมพู", this);

        _freeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, _tipWidth));

        // ตอนขึง: หนาที่สองปลาย บางตรงกลาง เหมือนเนื้อที่ถูกดึงจนยืด
        // กลางเส้นไม่บางเกิน 0.6 — บางกว่านี้มองไกล ๆ แล้วเส้นแตกเป็นจุด ๆ ดูไม่ติดกัน
        _tetherCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.6f), new Keyframe(1f, 0.85f));

        BuildStrands();
    }

    // ทุกครั้งที่เปิด (รวมตอนหยิบจาก pool) — รอเฟรมแรกค่อยรีเซ็ต
    // เพราะ pool มักเปิดก่อนแล้วค่อยย้ายไปที่ปาก ถ้ารีเซ็ตตอนนี้จะได้จุดเกิดผิดที่
    private void OnEnable()
    {
        _needsReset = true;
        _prevDt = 0f;
        if (_strands == null) return;
        foreach (var s in _strands) s.Line.enabled = false;
    }

    private void LateUpdate()
    {
        if (_strands == null) return;

        if (_needsReset)
        {
            ResetStrands();
            _needsReset = false;
        }

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);   // เฟรมกระตุกแล้วเส้นไม่ระเบิด
        if (dt <= 0f) return;                             // หยุดเกมอยู่
        if (_prevDt <= 0f) _prevDt = dt;

        Vector3 anchor = AnchorPosition();
        foreach (var s in _strands) Simulate(s, dt, anchor);

        _prevDt = dt;
    }

    // ---------- สร้าง / รีเซ็ต ----------

    private void BuildStrands()
    {
        _strands = new Strand[_strandCount];

        for (int i = 0; i < _strandCount; i++)
        {
            var go = new GameObject("FleshStrand_" + i);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = _pointsPerStrand;
            line.sharedMaterial = _material;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.generateLightingData = true;   // ให้ HDRP/Lit มีแสงเงา
            line.shadowCastingMode = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            line.widthCurve = _freeCurve;
            line.enabled = false;

            _strands[i] = new Strand
            {
                Line = line,
                Pos = new Vector3[_pointsPerStrand],
                Prev = new Vector3[_pointsPerStrand],
                Root = _roots != null && _roots.Length > 0 ? _roots[i % _roots.Length] : null,
                RootLocal = Random.onUnitSphere * _rootRadius,
                Seed = Random.Range(0f, 100f),
            };
        }
    }

    private void ResetStrands()
    {
        _spawnPoint = transform.position;
        _lastAnchorPoint = _spawnPoint;
        _foundAnchor = _tether == TetherMode.NearestAnchor
            ? FleshStrandAnchor.FindNearest(_spawnPoint, _anchorSearchRadius)
            : null;

        // บอกตอคอว่ามีก้อนมายึดแล้ว — ก้อนปลอมในมือ (HeldFleshChunk) จะได้หายตอนก้อนจริงออกมา
        if (_foundAnchor != null) _foundAnchor.NotifyClaimed(this);
        Vector3 anchor = AnchorPosition();
        bool tether = _tether != TetherMode.None;

        foreach (var s in _strands)
        {
            s.FreeLength = Random.Range(_lengthRange.x, _lengthRange.y);
            s.SnapAt = Random.Range(_snapDistance.x, _snapDistance.y);
            s.AnchorOffset = Random.insideUnitSphere * _anchorRadius;
            s.Tethered = tether;
            s.Recoil = 0f;
            s.FreeRootLocal = s.RootLocal;

            Vector3 root = RootPosition(s, anchor);
            Vector3 end = tether ? anchor + s.AnchorOffset : root + Vector3.down * s.FreeLength;
            int n = s.Pos.Length;
            for (int i = 0; i < n; i++)
            {
                s.Pos[i] = Vector3.Lerp(root, end, i / (n - 1f));
                s.Prev[i] = s.Pos[i];
            }

            s.Line.widthCurve = tether ? _tetherCurve : _freeCurve;
            s.Line.widthMultiplier = _rootWidth;
            s.Line.SetPositions(s.Pos);
            s.Line.enabled = true;
        }
    }

    // ---------- ฟิสิกส์ ----------

    private void Simulate(Strand s, float dt, Vector3 anchor)
    {
        int n = s.Pos.Length;
        Vector3 root = RootPosition(s, anchor);
        Vector3 tip = anchor + s.AnchorOffset;

        if (s.Tethered && Vector3.Distance(root, tip) > s.SnapAt)
            Snap(s, root, tip);   // โคนยังอยู่จุดเดิม — เฟรมต่อไปค่อยอ่านจาก FreeRootLocal

        float total;
        if (s.Tethered)
        {
            total = Vector3.Distance(root, tip) * (1f + _sag);
        }
        else
        {
            s.Recoil = Mathf.MoveTowards(s.Recoil, 0f, dt / _recoilTime);
            total = Mathf.Lerp(s.FreeLength, s.StretchedLength, s.Recoil);
        }
        float segment = total / (n - 1);

        // Verlet แบบชดเชยเฟรมเร็ว/ช้า — แรงต้านคิดต่อ 1/60 วิ ให้ผลเท่ากันทุก FPS
        float timeScale = dt / _prevDt;
        float drag = Mathf.Pow(1f - _damping, dt * 60f);
        Vector3 gravity = Physics.gravity * _gravityScale;
        float t = Time.time * _wiggleSpeed;
        int last = s.Tethered ? n - 2 : n - 1;

        for (int i = 1; i <= last; i++)
        {
            Vector3 p = s.Pos[i];
            Vector3 velocity = (p - s.Prev[i]) * (timeScale * drag);
            Vector3 accel = gravity + Wiggle(s.Seed, i, t) * _wiggle;
            s.Prev[i] = p;
            s.Pos[i] = p + velocity + accel * (dt * dt);
        }

        s.Pos[0] = root;
        s.Prev[0] = root;
        if (s.Tethered)
        {
            s.Pos[n - 1] = tip;
            s.Prev[n - 1] = tip;
        }

        // ดึงให้แต่ละท่อนยาวเท่าที่ควร
        for (int k = 0; k < _iterations; k++)
        {
            for (int i = 0; i < n - 1; i++)
            {
                bool pinA = i == 0;
                bool pinB = s.Tethered && i + 1 == n - 1;
                if (pinA && pinB) continue;

                Vector3 delta = s.Pos[i + 1] - s.Pos[i];
                float length = delta.magnitude;
                if (length < 1e-6f) continue;

                Vector3 fix = delta * ((length - segment) / length);
                if (pinA) s.Pos[i + 1] -= fix;
                else if (pinB) s.Pos[i] += fix;
                else
                {
                    s.Pos[i] += fix * 0.5f;
                    s.Pos[i + 1] -= fix * 0.5f;
                }
            }
        }

        // ยิ่งยืดยิ่งบาง (เนื้อเท่าเดิมแต่ยาวขึ้น)
        float stretch = (s.Tethered ? Vector3.Distance(root, tip) : total) / Mathf.Max(s.FreeLength, 0.001f);
        float thin = Mathf.Clamp(1f / Mathf.Sqrt(Mathf.Max(stretch, 1f)), _thinnestWhenStretched, 1f);
        s.Line.widthMultiplier = _rootWidth * thin;
        s.Line.SetPositions(s.Pos);
    }

    private void Snap(Strand s, Vector3 root, Vector3 tip)
    {
        s.Tethered = false;
        s.FreeRootLocal = Quaternion.Inverse(transform.rotation) * (root - transform.position);
        s.StretchedLength = Vector3.Distance(root, tip);
        s.Recoil = 1f;
        s.Line.widthCurve = _freeCurve;
        _onSnap?.Invoke(Vector3.Lerp(root, tip, 0.5f));
    }

    private static Vector3 Wiggle(float seed, int index, float t)
    {
        float o = seed + index * 0.37f;
        return new Vector3(
            Mathf.PerlinNoise(o, t) - 0.5f,
            Mathf.PerlinNoise(o + 13.1f, t) - 0.5f,
            Mathf.PerlinNoise(o + 27.7f, t) - 0.5f) * 2f;
    }

    // ---------- ตำแหน่ง ----------

    // ไม่คูณ scale — ก้อนเนื้อมักถูก scale แปลก ๆ (เช่น 12, 15, 12) รัศมีจะได้เป็นเมตรจริง
    // ตอนขึง: กลางกลุ่มโคนเลื่อนไปทางตอคอ (Root Toward Anchor) เส้นจะงอกจากปลายก้อนฝั่งคอ
    // หลังขาด: โคนติดอยู่ที่เดิมบนก้อน (จำไว้ตอนขาด) แล้วห้อยตามก้อนไป
    private Vector3 RootPosition(Strand s, Vector3 anchor)
    {
        if (s.Root != null) return s.Root.position;

        Vector3 center = transform.position;
        if (!s.Tethered) return center + transform.rotation * s.FreeRootLocal;

        if (_rootTowardAnchor > 0f)
        {
            Vector3 toAnchor = anchor - center;
            float distance = toAnchor.magnitude;
            // ก้อนอยู่ใกล้คอมาก ๆ — เลื่อนแค่ครึ่งทาง โคนจะได้ไม่เลยไปอีกฝั่งของคอ
            if (distance > 1e-4f)
                center += toAnchor * (Mathf.Min(_rootTowardAnchor, distance * 0.5f) / distance);
        }

        return center + transform.rotation * s.RootLocal;
    }

    private Vector3 AnchorPosition()
    {
        Transform target = null;
        if (_tether == TetherMode.Transform) target = _anchor;
        else if (_tether == TetherMode.NearestAnchor) target = _foundAnchor != null ? _foundAnchor.transform : null;
        else return _spawnPoint;

        if (target != null && target.gameObject.activeInHierarchy)
            _lastAnchorPoint = target.position;

        return _lastAnchorPoint;
    }

    private void OnValidate()
    {
        if (_lengthRange.y < _lengthRange.x) _lengthRange.y = _lengthRange.x;
        if (_snapDistance.y < _snapDistance.x) _snapDistance.y = _snapDistance.x;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        if (_roots != null && _roots.Length > 0)
        {
            foreach (var r in _roots)
                if (r != null) Gizmos.DrawSphere(r.position, 0.01f);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, _rootRadius);
        }

        if (_tether == TetherMode.Transform && _anchor != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(_anchor.position, _anchorRadius);
        }

        if (Application.isPlaying && _foundAnchor != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawLine(transform.position, _foundAnchor.transform.position);
        }
    }
}
