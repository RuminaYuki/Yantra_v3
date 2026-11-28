using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ดวงอาทิตย์คราส (สุริยุปราคา) บนฟ้า — แผ่นสี่เหลี่ยมหันหาผู้เล่นตลอด อยู่ไกลตามกล้องไป
/// ไม่ว่าเดินไปไหนก็อยู่ทิศเดิมบนฟ้าเหมือนดวงอาทิตย์จริง
/// BossIntro สั่ง Show(ทิศไปหาบอส) ตอนเริ่มสู้ → โผล่ด้านหลังบอส / Hide() ตอนบอสตาย
/// material: HDRP/Unlit แบบ Additive + Emission (สร้างให้ด้วยเมนู Setup Tani Boss Intro)
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class EclipseSun : MonoBehaviour
{
    [Tooltip("อยู่ห่างกล้องกี่เมตร — ไกลกว่า Far Clip ของกล้องจะถูกดึงเข้ามาเอง")]
    [SerializeField, Min(10f)] private float _distance = 300f;

    [Tooltip("สูงจากขอบฟ้ากี่องศา")]
    [SerializeField, Range(0f, 80f)] private float _elevation = 24f;

    [Tooltip("ขนาดบนฟ้า (องศา) ทั้งแผ่นรวมแสงรอบ ๆ — ดวงดำตรงกลางกว้างราว 1/4 ของค่านี้")]
    [SerializeField, Range(1f, 90f)] private float _angularSize = 34f;

    [Tooltip("หมุนแสงรอบดวงช้า ๆ (องศา / วินาที)")]
    [SerializeField] private float _spinSpeed = 3f;

    [Tooltip("แสงเต้นตุบ ๆ (0 = นิ่ง)")]
    [SerializeField, Range(0f, 0.5f)] private float _pulse = 0.12f;

    [SerializeField, Min(0.01f)] private float _fadeIn = 4f;
    [SerializeField, Min(0.01f)] private float _fadeOut = 3f;

    private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    private MeshRenderer _renderer;
    private MaterialPropertyBlock _block;
    private Color _baseUnlit = Color.white;
    private Color _baseEmissive = Color.white;
    private Vector3 _dir = Vector3.forward;
    private float _level;
    private float _target;
    private float _spin;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _block = new MaterialPropertyBlock();

        Material mat = _renderer.sharedMaterial;
        if (mat != null)
        {
            if (mat.HasProperty(UnlitColorId)) _baseUnlit = mat.GetColor(UnlitColorId);
            if (mat.HasProperty(EmissiveColorId)) _baseEmissive = mat.GetColor(EmissiveColorId);
        }

        // ตามกล้องทุกเฟรม — ไม่ให้เกิด motion blur ของวัตถุ
        _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;

        _level = 0f;
        _target = 0f;
        Apply(0f);
        _renderer.enabled = false;
    }

    /// <summary>โผล่บนฟ้าทางทิศนี้ (ใช้แค่ทิศแนวนอน แล้วยกขึ้นตาม Elevation)</summary>
    public void Show(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        direction.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, direction);
        _dir = Quaternion.AngleAxis(-_elevation, side) * direction;
        _target = 1f;
        _renderer.enabled = true;
    }

    public void Hide() => _target = 0f;

    private void LateUpdate()
    {
        float speed = _target > _level ? 1f / _fadeIn : 1f / _fadeOut;
        _level = Mathf.MoveTowards(_level, _target, speed * Time.deltaTime);

        if (_level <= 0f && _target <= 0f)
        {
            if (_renderer.enabled) _renderer.enabled = false;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        float dist = Mathf.Min(_distance, cam.farClipPlane * 0.9f);
        transform.position = cam.transform.position + _dir * dist;

        _spin += _spinSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(_dir) * Quaternion.Euler(0f, 0f, _spin);

        float size = 2f * dist * Mathf.Tan(_angularSize * 0.5f * Mathf.Deg2Rad);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(size / SafeScale(parentScale.x), size / SafeScale(parentScale.y),
                                           size / SafeScale(parentScale.z));

        float eased = _level * _level * (3f - 2f * _level);
        float pulse = 1f + _pulse * Mathf.Sin(Time.time * 1.7f) * Mathf.Sin(Time.time * 0.63f + 1.3f);
        Apply(eased * pulse);
    }

    private void Apply(float k)
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_block);
        _block.SetColor(UnlitColorId, _baseUnlit * k);
        _block.SetColor(EmissiveColorId, _baseEmissive * k);
        _renderer.SetPropertyBlock(_block);
    }

    private static float SafeScale(float s) => Mathf.Abs(s) < 0.0001f ? 1f : s;
}
