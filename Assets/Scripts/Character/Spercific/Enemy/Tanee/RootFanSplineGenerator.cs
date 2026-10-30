using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// สร้างเส้นตรงหลายเส้นกางเป็นพัด (Vengeful Roots - Pattern 1) ลงใน SplineContainer ตัวเดียว
/// - วาง component นี้บน root ของ pattern prefab (ตำแหน่งของ object นี้ = ตำแหน่งบอส)
/// - ทิศหน้าของพัด = แกน +Z ของ object นี้ (ระนาบ XZ, Y ชี้ขึ้น)
/// - เส้นที่ i = Spline i ใน container (knot อยู่ใน local space ของ object นี้)
/// - SplineInstantiate ตัวเดียวบน object นี้วางของได้ทุกเส้น
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineContainer))]
public class RootFanSplineGenerator : MonoBehaviour
{
    [Header("Lanes")]
    [Min(1)] public int laneCount = 3;

    [Tooltip("องศาระหว่างเส้นที่อยู่ติดกัน")]
    [Range(0f, 180f)] public float angleBetween = 30f;

    [Tooltip("หมุนทั้งพัด (0 = หันตามแกน +Z ของ object นี้)")]
    public float fanRotation = 0f;

    [Header("Distance")]
    [Tooltip("ระยะจากบอสถึงโคนราก")]
    [Min(0f)] public float startOffset = 1.5f;

    [Tooltip("ระยะห่างด้านข้างของโคนแต่ละเส้น (0 = ทุกเส้นเริ่มจากจุดเดียวกัน)")]
    [Min(0f)] public float rootSpacing = 0f;

    [Tooltip("ความยาวของแต่ละเส้น")]
    [Min(0.01f)] public float length = 8f;

    [Header("Per-lane tweaks (ไม่บังคับ — index ตรงกับ Spline i)")]
    public List<LaneTweak> perLane = new List<LaneTweak>();

    [Header("Preview")]
    [Tooltip("ความกว้างที่ใช้วาด gizmo (ไว้เทียบกับ telegraph/hitbox)")]
    [Min(0f)] public float previewWidth = 1.5f;
    public Color previewColor = new Color(1f, 0.2f, 0.2f, 0.9f);

    [System.Serializable]
    public struct LaneTweak
    {
        public float extraAngle;
        public float extraOffset;
        public float extraLength;
    }

    SplineContainer _container;
    SplineContainer Container => _container != null ? _container : _container = GetComponent<SplineContainer>();

    // ---------------------------------------------------------------- Math

    /// <summary>คำนวณตำแหน่ง/ทิศ/ความยาวของเส้นที่ i (local space ของ object นี้)</summary>
    public void ComputeLane(int i, out Vector3 localPos, out Quaternion localRot, out float laneLength)
    {
        LaneTweak tweak = i < perLane.Count ? perLane[i] : default;

        // index นับจากกลาง: 3 เส้น -> -1, 0, +1
        float c = i - (laneCount - 1) * 0.5f;

        float angle = fanRotation + c * angleBetween + tweak.extraAngle;
        localRot = Quaternion.Euler(0f, angle, 0f);

        Vector3 dir = localRot * Vector3.forward;
        Vector3 side = Quaternion.Euler(0f, fanRotation, 0f) * Vector3.right;

        localPos = side * (c * rootSpacing) + dir * (startOffset + tweak.extraOffset);
        laneLength = Mathf.Max(0.01f, length + tweak.extraLength);
    }

    // ---------------------------------------------------------------- Build

    [ContextMenu("Rebuild Lanes")]
    public void Rebuild()
    {
        SplineContainer container = Container;
        if (container == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RecordObject(container, "Rebuild Root Lanes");
#endif

        // เพิ่มเส้นที่ขาด / ลบเส้นที่เกิน (ใช้ Spline เดิมซ้ำ)
        while (container.Splines.Count < laneCount)
            container.AddSpline();
        while (container.Splines.Count > laneCount)
            container.RemoveSplineAt(container.Splines.Count - 1);

        for (int i = 0; i < laneCount; i++)
            ApplyLane(container.Splines[i], i);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(container);
#endif
    }

    void ApplyLane(Spline spline, int i)
    {
        ComputeLane(i, out Vector3 pos, out Quaternion rot, out float laneLength);

        // เส้นตรง 2 knot: โคน -> ปลาย (ทิศ/ระยะคิดไว้ใน knot เลย เพราะทุกเส้นใช้ transform เดียวกัน)
        Vector3 end = pos + rot * Vector3.forward * laneLength;

        spline.Clear();
        spline.Add(new BezierKnot((float3)pos), TangentMode.Linear);
        spline.Add(new BezierKnot((float3)end), TangentMode.Linear);
        spline.Closed = false;
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
        Matrix4x4 root = transform.localToWorldMatrix;

        // จุดบอส
        Gizmos.matrix = root;
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(Vector3.zero, 0.3f);

        // สี่เหลี่ยมของแต่ละเส้น
        Gizmos.color = previewColor;
        for (int i = 0; i < laneCount; i++)
        {
            ComputeLane(i, out Vector3 pos, out Quaternion rot, out float laneLength);
            Gizmos.matrix = root * Matrix4x4.TRS(pos, rot, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, 0f, laneLength * 0.5f),
                                new Vector3(previewWidth, 0.05f, laneLength));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
