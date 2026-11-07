using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// สร้างเส้นโค้งซ้อนกันหลายชั้นเป็นวงรอบบอส (Vengeful Roots - Pattern 2) ลงใน SplineContainer ตัวเดียว
/// - วาง component นี้บน root ของ pattern (ตำแหน่งของ object นี้ = ตำแหน่งบอส)
/// - ทิศหน้าของวง = แกน +Z ของ object นี้ (ระนาบ XZ, Y ชี้ขึ้น)
/// - วงที่ i = Spline i ใน container (วงใน -> วงนอก, knot อยู่ใน local space ของ object นี้)
/// - SplineInstantiate ตัวเดียวบน object นี้วางของได้ทุกวง
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineContainer))]
public class RootArcSplineGenerator : MonoBehaviour
{
    [Header("Rings")]
    [Min(1)] public int ringCount = 4;

    [Tooltip("ความกว้างของแต่ละวง (องศา, 360 = วงกลมเต็ม)")]
    [Range(1f, 360f)] public float arcAngle = 120f;

    [Tooltip("หมุนทุกวง (0 = กลางวงหันตามแกน +Z ของ object นี้)")]
    public float arcRotation = 0f;

    [Tooltip("จำนวนช่วงของแต่ละวง (มาก = โค้งเนียน)")]
    [Min(1)] public int segmentsPerRing = 8;

    [Header("Distance")]
    [Tooltip("รัศมีของวงในสุด (ระยะจากบอส)")]
    [Min(0f)] public float startRadius = 1.5f;

    [Tooltip("ระยะห่างระหว่างวงที่อยู่ติดกัน")]
    [Min(0f)] public float ringSpacing = 1.5f;

    [Header("Per-ring tweaks (ไม่บังคับ — index ตรงกับ Spline i)")]
    public List<RingTweak> perRing = new List<RingTweak>();

    [Header("Preview")]
    [Tooltip("ความกว้างที่ใช้วาด gizmo (ไว้เทียบกับ telegraph/hitbox)")]
    [Min(0f)] public float previewWidth = 1f;
    public Color previewColor = new Color(1f, 0.2f, 0.2f, 0.9f);

    [System.Serializable]
    public struct RingTweak
    {
        public float extraRadius;
        public float extraArcAngle;
        public float extraRotation;
    }

    SplineContainer _container;
    SplineContainer Container => _container != null ? _container : _container = GetComponent<SplineContainer>();

    // ---------------------------------------------------------------- Math

    /// <summary>คำนวณรัศมี/มุมเริ่ม/ความกว้างของวงที่ i (local space ของ object นี้)</summary>
    public void ComputeRing(int i, out float radius, out float startAngle, out float sweep)
    {
        RingTweak tweak = i < perRing.Count ? perRing[i] : default;

        radius = Mathf.Max(0.01f, startRadius + i * ringSpacing + tweak.extraRadius);
        sweep = Mathf.Clamp(arcAngle + tweak.extraArcAngle, 1f, 360f);

        // กลางวงอยู่ที่ arcRotation -> เริ่มจากครึ่งซ้าย
        startAngle = arcRotation + tweak.extraRotation - sweep * 0.5f;
    }

    /// <summary>จุดบนวงรัศมี radius ที่มุม angle (องศา, 0 = +Z)</summary>
    static Vector3 PointOnArc(float radius, float angle)
    {
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
    }

    static bool IsFullCircle(float sweep) => sweep >= 359.99f;

    // ---------------------------------------------------------------- Build

    [ContextMenu("Rebuild Rings")]
    public void Rebuild()
    {
        SplineContainer container = Container;
        if (container == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RecordObject(container, "Rebuild Root Rings");
#endif

        // เพิ่มเส้นที่ขาด / ลบเส้นที่เกิน (ใช้ Spline เดิมซ้ำ)
        while (container.Splines.Count < ringCount)
            container.AddSpline();
        while (container.Splines.Count > ringCount)
            container.RemoveSplineAt(container.Splines.Count - 1);

        for (int i = 0; i < ringCount; i++)
            ApplyRing(container.Splines[i], i);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(container);
#endif
    }

    void ApplyRing(Spline spline, int i)
    {
        ComputeRing(i, out float radius, out float startAngle, out float sweep);

        bool closed = IsFullCircle(sweep);
        // วงปิดไม่ต้องใส่ knot สุดท้ายซ้ำกับตัวแรก
        int knotCount = closed ? segmentsPerRing : segmentsPerRing + 1;
        float step = sweep / segmentsPerRing;

        spline.Clear();
        for (int k = 0; k < knotCount; k++)
            spline.Add(new BezierKnot((float3)PointOnArc(radius, startAngle + k * step)), TangentMode.AutoSmooth);
        spline.Closed = closed;
    }

    // ---------------------------------------------------------------- Editor auto-update

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        // แก้ component อื่นใน OnValidate ตรงๆ ไม่ปลอดภัย -> เลื่อนไปทำเฟรมถัดไป
        EditorApplication.delayCall -= DelayedRebuild;
        EditorApplication.delayCall += DelayedRebuild;
    }

    void DelayedRebuild()
    {
        if (this == null) return;                               // ถูกลบไปแล้ว
        if (PrefabUtility.IsPartOfPrefabAsset(this)) return;    // แก้ใน Prefab Mode แทน
        Rebuild();
    }
#endif

    // ---------------------------------------------------------------- Gizmos

    void OnDrawGizmos()
    {
        // จุดบอส
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(Vector3.zero, 0.3f);

        // ขอบใน/ขอบนอกของแต่ละวง (กว้าง previewWidth)
        Gizmos.color = previewColor;
        float halfWidth = previewWidth * 0.5f;
        for (int i = 0; i < ringCount; i++)
        {
            ComputeRing(i, out float radius, out float startAngle, out float sweep);
            DrawArc(radius - halfWidth, startAngle, sweep);
            DrawArc(radius + halfWidth, startAngle, sweep);

            // ปิดหัวท้ายของวงที่ไม่ครบรอบ
            if (!IsFullCircle(sweep))
            {
                float endAngle = startAngle + sweep;
                Gizmos.DrawLine(PointOnArc(radius - halfWidth, startAngle), PointOnArc(radius + halfWidth, startAngle));
                Gizmos.DrawLine(PointOnArc(radius - halfWidth, endAngle), PointOnArc(radius + halfWidth, endAngle));
            }
        }
        Gizmos.matrix = Matrix4x4.identity;
    }

    static void DrawArc(float radius, float startAngle, float sweep)
    {
        if (radius <= 0f) return;

        // gizmo ใช้ช่วงละเอียดกว่า spline เพื่อให้เห็นโค้งจริง
        const int steps = 32;
        float step = sweep / steps;
        Vector3 prev = PointOnArc(radius, startAngle);
        for (int s = 1; s <= steps; s++)
        {
            Vector3 next = PointOnArc(radius, startAngle + s * step);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
