using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;

// ค่อยๆ จาง/ปรากฏ Decal ทีละตัว: ก๊อป material ให้ projector ตัวนี้ตัวเดียว แล้วไล่ค่า opacity ใน shader
// DecalProjector ไม่มี MaterialPropertyBlock และ .material เป็น shared → ต้อง new Material เอง
[DisallowMultipleComponent]
public class DecalOpacityFader : MonoBehaviour
{
    [Tooltip("เว้นว่าง = หา DecalProjector บน GameObject นี้")]
    [SerializeField] private DecalProjector _projector;

    [Tooltip("ชื่อ Reference ใน Shader Graph (ไม่ใช่ชื่อที่โชว์) เช่น Opacity → _Opacity")]
    [SerializeField] private string _opacityProperty = "_Opacity";

    [Header("เวลา")]
    [SerializeField, Min(0f)] private float _delay = 0f;
    [SerializeField, Min(0.01f)] private float _duration = 1f;
    [Tooltip("ค่าตอนเห็นชัด (Nak Hold: Alpha = Texture - Opacity → 0 = เห็นชัด)")]
    [SerializeField] private float _visibleValue = 0f;
    [Tooltip("ค่าตอนหายหมด")]
    [SerializeField] private float _hiddenValue = 1f;

    public enum EnableAction { None, FadeOut, FadeIn }

    [Header("ตอนเริ่ม")]
    [Tooltip("เปิด object แล้วให้ทำอะไรเอง\nNone = รอคนเรียก FadeOut()/FadeIn()\nFade Out = ค่อยๆ หาย\nFade In = ค่อยๆ โผล่")]
    [FormerlySerializedAs("_fadeOutOnEnable")]
    [SerializeField] private EnableAction _onEnable = EnableAction.None;

    [Header("Event (ไม่ใส่ก็ได้)")]
    [SerializeField] private UnityEvent _onFinished = new UnityEvent();

    private Material _original;
    private Material _instance;
    private int _opacityId;
    private Coroutine _routine;

    public bool IsFading => _routine != null;

    private void Awake()
    {
        if (_projector == null)
            _projector = GetComponent<DecalProjector>();
        _opacityId = Shader.PropertyToID(_opacityProperty);
    }

    private void OnEnable()
    {
        switch (_onEnable)
        {
            case EnableAction.FadeOut:
                FadeOut();
                break;
            case EnableAction.FadeIn:
                FadeIn();
                break;
        }
    }

    private void OnDisable() => ResetFade();
    private void OnDestroy() => ResetFade();

    /// <summary>ค่อยๆ จางหาย (visible → hidden)</summary>
    [ContextMenu("Fade Out")]
    public void FadeOut() => Fade(_visibleValue, _hiddenValue, _delay, _duration);

    /// <summary>ค่อยๆ ปรากฏ (hidden → visible)</summary>
    [ContextMenu("Fade In")]
    public void FadeIn() => Fade(_hiddenValue, _visibleValue, _delay, _duration);

    /// <summary>หยุด แล้วคืน material เดิม + เปิด projector กลับ</summary>
    [ContextMenu("Reset Fade")]
    public void ResetFade()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        RestoreMaterial();
        if (_projector != null)
            _projector.enabled = true;
    }

    /// <summary>ไล่ค่า opacity from → to ด้วยเวลาที่ส่งมาเอง (ไม่ใช้ค่าใน Inspector) เช่น จาก State Action</summary>
    public void Fade(float from, float to, float delay, float duration)
    {
        if (!isActiveAndEnabled || !PrepareMaterial())
            return;

        if (_routine != null)
            StopCoroutine(_routine);
        _projector.enabled = true;
        _routine = StartCoroutine(Routine(from, to, Mathf.Max(0f, delay), Mathf.Max(0.01f, duration)));
    }

    private bool PrepareMaterial()
    {
        if (_instance != null)
            return true;   // ก๊อปไว้แล้ว ใช้ต่อได้เลย

        if (_projector == null || _projector.material == null)
        {
            Debug.LogWarning("[Decal Fade] ไม่พบ DecalProjector หรือ Material", this);
            return false;
        }
        if (!_projector.material.HasFloat(_opacityId))
        {
            Debug.LogWarning($"[Decal Fade] Material '{_projector.material.name}' ไม่มี {_opacityProperty} " +
                             "(เช็คชื่อ Reference ใน Shader Graph)", this);
            return false;
        }

        _original = _projector.material;
        _instance = new Material(_original) { name = _original.name + " (Fade instance)" };
        _projector.material = _instance;
        return true;
    }

    private IEnumerator Routine(float from, float to, float delay, float duration)
    {
        _instance.SetFloat(_opacityId, from);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _instance.SetFloat(_opacityId, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        _instance.SetFloat(_opacityId, to);

        // ไม่คืน material เดิมตรงนี้ ให้ตัวก๊อปค้างค่าสุดท้ายไว้ (คืนตอน ResetFade / ปิด object)

        _routine = null;
        _onFinished?.Invoke();
    }

    private void RestoreMaterial()
    {
        if (_instance == null)
            return;
        if (_projector != null)
            _projector.material = _original;
        Destroy(_instance);
        _instance = null;
        _original = null;
    }
}
