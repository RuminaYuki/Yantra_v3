using UnityEngine;

/// <summary>
/// ให้กล้องมองไกลได้เฉพาะบาง layer (เช่น Backdrop) ส่วนของอื่นยังถูกตัดที่ระยะเดิม
///
/// ทำไมต้องทำแบบนี้:
/// backdrop แผ่นใหญ่โดน Far Clip ของกล้องตัดเป็นเส้นตรง
/// แต่ถ้าดัน Far ของกล้องขึ้นเฉยๆ ทุกอย่างในฉากจะถูกวาดไกลขึ้นด้วย → FPS ตก
/// เลยตั้ง Far ให้ไกล แล้วใช้ layerCullDistances จำกัด layer อื่นไว้ที่ระยะเดิม
///
/// วิธีใช้:
/// 1. ตั้ง Far Clip Plane ที่ Lens ของ CinemachineCamera (ไม่ใช่ที่ Camera — Cinemachine เขียนทับทุกเฟรม)
/// 2. ใส่สคริปต์นี้บน GameObject ที่มี Camera จริง แล้วเลือก layer ที่ให้มองไกลใน Far Layers
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraLayerCull : MonoBehaviour
{
    [Tooltip("ระยะตัดของ layer ทั่วไป — ตั้งเท่า Far เดิมของกล้อง")]
    [SerializeField] private float _defaultCullDistance = 200f;

    [Tooltip("layer ที่มองได้ไกลถึง Far ของกล้อง (เช่น Backdrop)")]
    [SerializeField] private LayerMask _farLayers;

    [Tooltip("true = วัดระยะเป็นวงกลมรอบกล้อง\nหมุนกล้องแล้วของที่ขอบจอจะไม่โผล่/หายเอง")]
    [SerializeField] private bool _spherical = true;

    private readonly float[] _distances = new float[32];

    private void OnEnable() => Apply();

#if UNITY_EDITOR
    // ปรับค่าใน Inspector ตอนกด Play แล้วเห็นผลทันที
    private void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }
#endif

    private void Apply()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        for (int layer = 0; layer < 32; layer++)
        {
            bool isFarLayer = (_farLayers.value & (1 << layer)) != 0;

            // 0 = ไม่จำกัด ใช้ Far ของกล้องตรงๆ
            _distances[layer] = isFarLayer ? 0f : _defaultCullDistance;
        }

        cam.layerCullDistances = _distances;
        cam.layerCullSpherical = _spherical;
    }
}
