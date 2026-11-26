using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// วาดกรอบ Local Volumetric Fog (เช่น Cloudy Sample) ตลอดเวลา แม้ไม่ได้ select
// HDRP วาดกรอบให้เฉพาะตอน selected อยู่แล้ว สคริปต์นี้เลยวาดแค่ตอน NonSelected
public static class LocalFogAlwaysGizmo
{
    static readonly Color WireColor = new Color(0.4f, 0.8f, 1f, 1f);

    const float HandleRadius = 1f;

    // Pickable = คลิกที่ gizmo แล้วเลือก GameObject นั้นได้
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Pickable)]
    static void Draw(LocalVolumetricFog fog, GizmoType type)
    {
        Transform t = fog.transform;

        // ลูกกลมตรงกลางไว้เป็นจุดคลิก (เส้น wire บางเกินจะคลิกโดน)
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = WireColor;
        Gizmos.DrawSphere(t.position, HandleRadius);

        // Scale Invariant = ไม่สน scale ของ Transform, Inherit From Hierarchy = ใช้ lossyScale
        Vector3 scale = fog.parameters.scaleMode == LocalVolumetricFogScaleMode.InheritFromHierarchy
            ? t.lossyScale
            : Vector3.one;

        Gizmos.color = WireColor;
        Gizmos.matrix = Matrix4x4.TRS(t.position, t.rotation, scale);
        Gizmos.DrawWireCube(Vector3.zero, fog.parameters.size);
    }
}
