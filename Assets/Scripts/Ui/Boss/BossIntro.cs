using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// ฉากเปิดตัวบอส — ไม่ต้องแก้ table: รอให้ Animator ของบอสเข้า state เริ่มสู้ครั้งแรก (ตานี = Jump_ShowDown)
///   1. กล้องสั่น + Volume สุริยุปราคาค่อย ๆ เข้ม + ดวงอาทิตย์คราสโผล่บนฟ้าด้านหลังบอส
///   2. ชื่อบอสขึ้นกลางจอ แล้วจางหาย
///   3. หลอดเลือดบอสค้างไว้จนบอสตาย
/// บอสตาย = หลอดเลือดหาย + ฟ้ากลับมาปกติ
///   4. หนามล้อมลาน + ใบกล้วยร่วง
/// สร้าง / ต่อทุกอย่างด้วยเมนู Tools/Yantra/Setup (ใช้ครั้งเดียว)/Setup Tani Boss Intro
/// </summary>
[DisallowMultipleComponent]
public class BossIntro : MonoBehaviour
{
    [Header("บอส")]
    [Tooltip("Animator ของบอส (ตัวเดียวกับ Paired Animation Actor)")]
    [SerializeField] private Animator _animator;

    [Tooltip("state ใน Animator ที่แปลว่าเริ่มสู้ — นับครั้งแรกครั้งเดียว")]
    [SerializeField] private string _startState = "Jump_ShowDown";

    [SerializeField] private Health _health;

    [Header("ชื่อบอส")]
    [SerializeField] private CanvasGroup _title;
    [SerializeField] private CanvasGroup _subtitle;

    [Tooltip("เริ่มสู้แล้วรอกี่วินาทีค่อยขึ้นชื่อ (ให้เสียงกรีดร้องนำไปก่อน)")]
    [SerializeField, Min(0f)] private float _titleDelay = 0.8f;
    [SerializeField, Min(0.01f)] private float _titleFadeIn = 0.9f;

    [Tooltip("ค้างชื่อไว้กี่วินาที")]
    [SerializeField, Min(0f)] private float _titleHold = 2.8f;
    [SerializeField, Min(0.01f)] private float _titleFadeOut = 1.2f;

    [Tooltip("คำโปรยขึ้นตามหลังชื่อกี่วินาที")]
    [SerializeField, Min(0f)] private float _subtitleLag = 0.45f;

    [Tooltip("ชื่อหดจากขนาดนี้ลงมาเหลือ 1 ตอนจางเข้า")]
    [SerializeField, Min(1f)] private float _titleStartScale = 1.15f;

    [Tooltip("เสียงตอนชื่อขึ้น (ไม่ใส่ก็ได้)")]
    [SerializeField] private SoundID _titleSound;

    [Header("หลอดเลือด")]
    [SerializeField] private CanvasGroup _bar;
    [SerializeField] private Image _barFill;
    [SerializeField] private Image _barTrail;

    [Tooltip("เริ่มสู้แล้วรอกี่วินาทีค่อยขึ้นหลอดเลือด")]
    [SerializeField, Min(0f)] private float _barDelay = 1.6f;
    [SerializeField, Min(0.01f)] private float _barFadeIn = 0.8f;

    [Tooltip("แถบสีจางที่ไล่ตามเลือดที่หายไป — โดนแล้วรอกี่วินาทีค่อยไล่")]
    [SerializeField, Min(0f)] private float _trailDelay = 0.5f;

    [Tooltip("แถบสีจางไล่ลงเร็วแค่ไหน (ทั้งหลอด / วินาที)")]
    [SerializeField, Min(0.01f)] private float _trailSpeed = 0.6f;

    [Header("ฟ้า (สุริยุปราคา)")]
    [SerializeField] private Volume _volume;
    [SerializeField, Min(0.01f)] private float _volumeFadeIn = 3f;
    [SerializeField, Min(0.01f)] private float _volumeFadeOut = 4f;
    [SerializeField] private EclipseSun _sun;

    [Header("ลาน")]
    [Tooltip("หนามล้อมลาน — ขึ้นตอนเริ่มสู้ หักลงตอนบอสตาย")]
    [SerializeField] private ArenaThornWall _arenaWall;

    [Tooltip("ใบกล้วยร่วงรอบตัวผู้เล่น — เริ่มตอนเริ่มสู้ หยุดตอนบอสตาย")]
    [SerializeField] private ParticleSystem _leaves;

    [Tooltip("ใบร่วงลงมาจากเหนือหัวผู้เล่นกี่เมตร")]
    [SerializeField] private float _leavesHeight = 9f;

    [Header("กล้องสั่นตอนเริ่ม")]
    [SerializeField, Range(0f, 1f)] private float _shakeTrauma = 0.55f;

    [Header("Debug")]
    [SerializeField] private bool _log;

    private int _startHash;
    private bool _started;
    private bool _ended;
    private float _trailHoldUntil;
    private Coroutine _volumeRoutine;

    private void Awake()
    {
        _startHash = string.IsNullOrEmpty(_startState) ? 0 : Animator.StringToHash(_startState);
        ResetVisuals();
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnHealthChanged += OnHealthChanged;
            _health.OnDead += OnBossDead;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnHealthChanged -= OnHealthChanged;
            _health.OnDead -= OnBossDead;
        }

        // บอสถูกปิด (เช่น รีเซ็ตด่าน) — เคลียร์ให้สะอาด เปิดใหม่จะเล่นเปิดตัวได้อีก
        if (!_ended)
        {
            StopAllCoroutines();
            _started = false;
            ResetVisuals();
            if (_arenaWall != null) _arenaWall.Lower();
            if (_leaves != null) _leaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        if (!_started && !_ended && IsInStartState())
            Begin();

        UpdateTrail();
    }

    private void LateUpdate()
    {
        if (_leaves != null && _leaves.isPlaying) FollowCamera();
    }

    // ตัวปล่อยใบไม้ตามกล้อง (ใบที่ร่วงแล้วอยู่ในโลก ไม่ลอยตาม)
    private void FollowCamera()
    {
        Camera cam = Camera.main;
        if (cam != null) _leaves.transform.position = cam.transform.position + Vector3.up * _leavesHeight;
    }

    /// <summary>เล่นฉากเปิดตัวทันที — ใช้ทดสอบ หรือให้ตัวอื่นสั่ง</summary>
    [ContextMenu("Play Intro (ทดสอบ)")]
    public void Begin()
    {
        if (_started || _ended) return;
        _started = true;
        if (_log) Debug.Log("[BossIntro] เริ่มสู้ — เปิดตัวบอส", this);

        if (CameraShaker.Instance != null && _shakeTrauma > 0f)
            CameraShaker.Instance.AddTrauma(_shakeTrauma);

        if (_sun != null)
        {
            Camera cam = Camera.main;
            Vector3 bossPos = _animator != null ? _animator.transform.position : transform.position;
            Vector3 toBoss = cam != null ? bossPos - cam.transform.position : transform.forward;
            _sun.Show(toBoss);
        }

        FadeVolume(1f, _volumeFadeIn);
        if (_arenaWall != null) _arenaWall.Raise();
        if (_leaves != null)
        {
            FollowCamera();
            _leaves.Play(true);
        }
        RefreshBar(true);
        StartCoroutine(TitleRoutine());
        StartCoroutine(BarRoutine());
    }

    private bool IsInStartState()
    {
        if (_startHash == 0 || _animator == null || !_animator.isActiveAndEnabled) return false;
        if (_animator.runtimeAnimatorController == null) return false;

        return _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0).shortNameHash == _startHash
            : _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _startHash;
    }

    // ---------- ชื่อบอส ----------

    private IEnumerator TitleRoutine()
    {
        yield return Wait(_titleDelay);

        if (_titleSound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlayEventSFX(_titleSound);

        Transform titleTransform = _title != null ? _title.transform : null;
        float total = _titleFadeIn + _subtitleLag;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float a = Mathf.Clamp01(t / _titleFadeIn);
            float b = Mathf.Clamp01((t - _subtitleLag) / _titleFadeIn);
            SetAlpha(_title, a);
            SetAlpha(_subtitle, b);
            if (titleTransform != null)
                titleTransform.localScale = Vector3.one * Mathf.Lerp(_titleStartScale, 1f, 1f - (1f - a) * (1f - a));
            yield return null;
        }
        SetAlpha(_title, 1f);
        SetAlpha(_subtitle, 1f);
        if (titleTransform != null) titleTransform.localScale = Vector3.one;

        yield return Wait(_titleHold);

        for (float t = 0f; t < _titleFadeOut; t += Time.deltaTime)
        {
            float a = 1f - t / _titleFadeOut;
            SetAlpha(_title, a);
            SetAlpha(_subtitle, a);
            yield return null;
        }
        SetAlpha(_title, 0f);
        SetAlpha(_subtitle, 0f);
    }

    // ---------- หลอดเลือด ----------

    private IEnumerator BarRoutine()
    {
        yield return Wait(_barDelay);
        yield return FadeGroup(_bar, 1f, _barFadeIn);
    }

    private void OnHealthChanged(float hp) => RefreshBar(false);

    private void RefreshBar(bool snap)
    {
        if (_health == null || _barFill == null) return;

        float max = Mathf.Max(0.0001f, _health.MaxHealth);
        float ratio = Mathf.Clamp01(_health.CurrentHP / max);
        bool lost = ratio < _barFill.fillAmount;
        _barFill.fillAmount = ratio;

        if (_barTrail == null) return;
        if (snap || !lost || _barTrail.fillAmount < ratio) _barTrail.fillAmount = ratio;   // เลือดเพิ่ม = ตามทันที
        else _trailHoldUntil = Time.time + _trailDelay;
    }

    private void UpdateTrail()
    {
        if (_barTrail == null || _barFill == null || Time.time < _trailHoldUntil) return;
        if (_barTrail.fillAmount > _barFill.fillAmount)
            _barTrail.fillAmount = Mathf.MoveTowards(_barTrail.fillAmount, _barFill.fillAmount, _trailSpeed * Time.deltaTime);
    }

    // ---------- จบไฟต์ ----------

    private void OnBossDead()
    {
        if (_ended) return;
        _ended = true;
        if (_log) Debug.Log("[BossIntro] บอสตาย — ฟ้ากลับมาปกติ", this);

        // หลุดออกจากตัวบอสก่อน — บอสโดนลบ / ปิดไป เอฟเฟกต์จะได้ค่อย ๆ จางจนจบ ไม่หายวับ
        transform.SetParent(null, true);
        StopAllCoroutines();
        StartCoroutine(EndRoutine());
    }

    private IEnumerator EndRoutine()
    {
        if (_sun != null) _sun.Hide();
        if (_arenaWall != null) _arenaWall.Lower();
        if (_leaves != null) _leaves.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        FadeVolume(0f, _volumeFadeOut);
        StartCoroutine(FadeGroup(_title, 0f, 0.4f));
        StartCoroutine(FadeGroup(_subtitle, 0f, 0.4f));
        yield return FadeGroup(_bar, 0f, 1.2f);
        yield return Wait(Mathf.Max(_volumeFadeOut, 6f));   // รอใบไม้ที่ร่วงอยู่ลงพื้นก่อน
        Destroy(gameObject);
    }

    // ---------- ตัวช่วย ----------

    private void ResetVisuals()
    {
        SetAlpha(_title, 0f);
        SetAlpha(_subtitle, 0f);
        SetAlpha(_bar, 0f);
        if (_volume != null) _volume.weight = 0f;
        if (_barFill != null) _barFill.fillAmount = 1f;
        if (_barTrail != null) _barTrail.fillAmount = 1f;
    }

    private void FadeVolume(float target, float time)
    {
        if (_volume == null) return;
        if (_volumeRoutine != null) StopCoroutine(_volumeRoutine);
        _volumeRoutine = StartCoroutine(VolumeRoutine(target, time));
    }

    private IEnumerator VolumeRoutine(float target, float time)
    {
        float from = _volume.weight;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            _volume.weight = Mathf.Lerp(from, target, k * k * (3f - 2f * k));
            yield return null;
        }
        _volume.weight = target;
        _volumeRoutine = null;
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float target, float time)
    {
        if (group == null) yield break;
        float from = group.alpha;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(from, target, t / time);
            yield return null;
        }
        group.alpha = target;
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
            yield return null;
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null) group.alpha = alpha;
    }
}
