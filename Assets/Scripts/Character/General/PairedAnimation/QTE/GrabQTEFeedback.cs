using UnityEngine;

// วางบน GameObject เดียวกับ GrabQTEInteractAddition (GrabQTE) — จอสั่น / จอแดงตอนโดนผีจับ ไม่ต้องลากอะไรใส่
[RequireComponent(typeof(GrabQTEInteractAddition))]
public class GrabQTEFeedback : MonoBehaviour
{
    [Header("ตอนผีจับ")]
    [Tooltip("กระชากกล้องแรงแค่ไหน (เมตร) — 0 = ไม่กระชาก")]
    [SerializeField, Min(0f)] private float _grabKick = 0.04f;
    [SerializeField, Min(0f)] private float _grabKickTime = 0.3f;

    [Tooltip("กล้องเอียงกี่องศาตอนกระชาก")]
    [SerializeField, Min(0f)] private float _grabKickTilt = 2f;

    [Header("ระหว่างโดนจับ")]
    [Tooltip("จอสั่นค้างไว้ตลอดตอนโดนจับ — 0 = ไม่สั่น / 1 = สั่นแรงสุด")]
    [SerializeField, Range(0f, 1f)] private float _holdShake = 0.25f;

    [Header("กด E")]
    [Tooltip("จอกระตุกทุกครั้งที่กด — 0 = ไม่กระตุก")]
    [SerializeField, Min(0f)] private float _mashKick = 0.01f;
    [SerializeField, Min(0f)] private float _mashKickTime = 0.08f;
    [SerializeField, Min(0f)] private float _mashKickTilt = 0.5f;

    [Header("โดนตัดเลือด")]
    [Tooltip("ติ๊ก = จอแดงทุกครั้งที่เลือดลด (แผ่น HurtFlash ตัวเดียวกับตอนโดนตี)")]
    [SerializeField] private bool _flashOnDamage = true;

    [Tooltip("จอแดงเข้มแค่ไหน")]
    [SerializeField, Range(0f, 1f)] private float _damageFlash = 0.3f;

    [Tooltip("สั่นเพิ่มทุกครั้งที่เลือดลด — 0 = ไม่สั่น")]
    [SerializeField, Range(0f, 1f)] private float _damageShake = 0.12f;

    [Header("ตอนจบ")]
    [Tooltip("กระชากตอนดิ้นหลุด — 0 = ไม่กระชาก")]
    [SerializeField, Min(0f)] private float _escapeKick = 0.04f;
    [SerializeField, Min(0f)] private float _escapeKickTime = 0.25f;

    [Tooltip("ความแรงตอนแพ้ (จอแดง + สั่น)")]
    [SerializeField, Range(0f, 1f)] private float _failStrength = 0.6f;

    private GrabQTEInteractAddition _qte;
    private bool _warned;

    private void Awake()
    {
        _qte = GetComponent<GrabQTEInteractAddition>();
    }

    private void OnEnable()
    {
        _qte.OnStarted.AddListener(HandleStarted);
        _qte.OnMash.AddListener(HandleMash);
        _qte.OnDamageTick.AddListener(HandleDamage);
        _qte.OnEscaped.AddListener(HandleEscaped);
        _qte.OnFailed.AddListener(HandleFailed);
    }

    private void OnDisable()
    {
        _qte.OnStarted.RemoveListener(HandleStarted);
        _qte.OnMash.RemoveListener(HandleMash);
        _qte.OnDamageTick.RemoveListener(HandleDamage);
        _qte.OnEscaped.RemoveListener(HandleEscaped);
        _qte.OnFailed.RemoveListener(HandleFailed);
    }

    // เติมให้สั่นค้างไว้ระดับเดิม — Pause อยู่ไม่เติม
    private void Update()
    {
        if (_holdShake <= 0f || !_qte.IsMashing || Time.timeScale <= 0f)
            return;

        CameraShaker shaker = CameraShaker.Instance;
        if (shaker != null && shaker.Trauma < _holdShake)
            shaker.AddTrauma(_holdShake - shaker.Trauma);
    }

    private void HandleStarted() => Kick(_grabKick, _grabKickTime, _grabKickTilt);
    private void HandleMash() => Kick(_mashKick, _mashKickTime, _mashKickTilt);
    private void HandleEscaped() => Kick(_escapeKick, _escapeKickTime, _grabKickTilt);

    // แยกจอแดงกับการสั่น — HurtFeedback.Play สั่นแรงเกินไปถ้าโดนทุกครึ่งวินาที
    private void HandleDamage()
    {
        if (_flashOnDamage && HasHurtFeedback())
            HurtFeedback.Instance.Flash(_damageFlash);

        if (_damageShake > 0f && CameraShaker.Instance != null)
            CameraShaker.Instance.AddTrauma(_damageShake);
    }

    private void HandleFailed()
    {
        if (HasHurtFeedback())
            HurtFeedback.Instance.Play(_failStrength);
        else if (CameraShaker.Instance != null)
            CameraShaker.Instance.AddTrauma(Mathf.Lerp(0.25f, 0.75f, _failStrength));
    }

    private bool HasHurtFeedback()
    {
        if (HurtFeedback.Instance != null)
            return true;

        if (!_warned)
        {
            _warned = true;
            Debug.LogWarning("[GrabQTEFeedback] ไม่พบ HurtFeedback ในฉาก — จอจะไม่แดง มีแค่จอสั่น", this);
        }
        return false;
    }

    private static void Kick(float radius, float duration, float tilt)
    {
        if (radius > 0f && CameraShaker.Instance != null)
            CameraShaker.Instance.Kick(radius, duration, tilt);
    }
}