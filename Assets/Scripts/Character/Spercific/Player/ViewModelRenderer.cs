using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// กล้องเฉพาะแขน (View Model) สำหรับ HDRP
///
/// ทำไมต้องทำแบบนี้:
/// HDRP ไม่มี camera stacking แบบ Built-in/URP — ตั้ง Clear Flags = Depth Only
/// แล้วให้กล้อง 2 วาดทับกล้อง 1 ไม่ได้ มันจะขึ้นแค่กล้องเดียว
/// ทางที่ใช้ได้คือให้กล้องแขนวาดลง RenderTexture แล้วเอาภาพนั้นมาแปะทับด้วย UI
///
/// ได้อะไร:
/// - แขนมี FOV เป็นของตัวเอง ไม่ยืดตามกล้องหลัก และไม่ลอยตอนเล็ง
/// - แขนมี Near Clip เป็นของตัวเอง เข้าใกล้กล้องได้โดยไม่โดนตัด
/// - แขนไม่ทะลุกำแพง เพราะวาดแยกคนละรอบกับฉาก
///
/// สคริปต์นี้ดูแล RenderTexture ให้เอง — สร้างตามขนาดจอจริง
/// และสร้างใหม่เมื่อผู้เล่นเปลี่ยนความละเอียด ไม่งั้นภาพแขนจะเบลอหรือยืด
/// </summary>
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]   // ให้ทำงานหลังกล้องหลักถูกขยับเสร็จแล้ว
public class ViewModelRenderer : MonoBehaviour
{
    [Header("ปลายทาง")]
    [Tooltip("RawImage เต็มจอที่จะเอาภาพแขนไปแปะ\nอยู่ใน Canvas แบบ Screen Space - Overlay")]
    [SerializeField] private RawImage _output;

    [Header("เลนส์ของกล้องแขน")]
    [Tooltip("FOV ของแขนอย่างเดียว ไม่เกี่ยวกับกล้องหลัก\n" +
             "ยิ่งแคบ แขนยิ่งดูแบนและบิดน้อยลง — เริ่มที่ 45 แล้วค่อยจูน")]
    [Range(20f, 90f)][SerializeField] private float _fieldOfView = 45f;

    [Tooltip("ระยะใกล้สุดที่กล้องแขนมองเห็น\nตั้งเล็กมากได้ เพราะกล้องนี้ไม่ได้มองฉากไกล")]
    [SerializeField] private float _nearClip = 0.01f;

    [Tooltip("ระยะไกลสุด — แขนอยู่ใกล้ตัวอยู่แล้ว ไม่ต้องตั้งเยอะ ยิ่งน้อยยิ่งประหยัด")]
    [SerializeField] private float _farClip = 10f;

    [Header("ความละเอียด")]
    [Tooltip("1 = เท่าจอจริง / 0.75 = ลดลงเพื่อประหยัด\n" +
             "แขนเป็นของใกล้ตัว ลดได้นิดหน่อยโดยแทบไม่เห็นความต่าง")]
    [Range(0.5f, 1f)][SerializeField] private float _resolutionScale = 1f;

    [Header("Debug")]
    [SerializeField] private bool _logTextureRebuild = false;

    private Camera _camera;
    private RenderTexture _renderTexture;
    private int _lastWidth;
    private int _lastHeight;

    public Camera Camera => _camera;

    /// <summary>เปลี่ยน FOV ของแขนตอนรัน เผื่ออยากให้แขนซูมตอนเล็งด้วย</summary>
    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            _fieldOfView = Mathf.Clamp(value, 20f, 90f);
            if (_camera != null) _camera.fieldOfView = _fieldOfView;
        }
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        ApplyLens();
    }

    private void OnEnable()
    {
        EnsureRenderTexture();
    }

    private void OnDisable()
    {
        ReleaseRenderTexture();
    }

    private void LateUpdate()
    {
        // จอเปลี่ยนขนาดได้ทุกเมื่อ (ย่อหน้าต่าง / เปลี่ยน resolution ในเมนู Setting ของเรา)
        // ถ้าไม่สร้างใหม่ ภาพแขนจะถูกยืดให้พอดีจอแล้วดูเบลอ
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            EnsureRenderTexture();
    }

    private void ApplyLens()
    {
        if (_camera == null) return;

        _camera.fieldOfView = _fieldOfView;
        _camera.nearClipPlane = _nearClip;
        _camera.farClipPlane = _farClip;

        // กล้องแขนไม่ใช่กล้องหลัก ห้ามเป็น MainCamera ไม่งั้นระบบอื่นจะหยิบผิดตัว
        if (_camera.CompareTag("MainCamera")) _camera.tag = "Untagged";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        ApplyLens();
    }
#endif

    // ---------- RenderTexture ----------

    private void EnsureRenderTexture()
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * _resolutionScale));
        int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * _resolutionScale));

        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        ReleaseRenderTexture();

        // ARGBHalf = มีช่อง alpha ซึ่งจำเป็น
        // ที่ว่างรอบแขนต้องโปร่งใส ไม่งั้นจะได้สี่เหลี่ยมดำทับทั้งจอ
        _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
        {
            name = "RT_ViewModel",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        _renderTexture.Create();

        if (_camera != null) _camera.targetTexture = _renderTexture;
        if (_output != null) _output.texture = _renderTexture;

        if (_logTextureRebuild)
            Debug.Log($"[ViewModel] สร้าง RenderTexture ใหม่ {width}x{height}", this);
    }

    private void ReleaseRenderTexture()
    {
        if (_renderTexture == null) return;

        if (_camera != null && _camera.targetTexture == _renderTexture)
            _camera.targetTexture = null;

        if (_output != null && _output.texture == _renderTexture)
            _output.texture = null;

        _renderTexture.Release();
        Destroy(_renderTexture);
        _renderTexture = null;
    }
}
