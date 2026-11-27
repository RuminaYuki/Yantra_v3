using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เสียงฝูงนกที่เป็น Particle — แปะบน GameObject ที่มี Particle System (เช่น CrowFlock)
///   1. นกเริ่มบิน (particle โผล่ตัวแรก) → "พรึ่บ!" ปีกกระพือพร้อมกัน 1 ครั้ง
///   2. การ้องตามหลังอีกไม่กี่ที สุ่มเวลา
///   3. เสียงปีกระหว่างบิน (ลูป) — เบาลงตามจำนวนนกที่เหลือ หมดฝูง = เงียบเอง
/// เสียงลอยตามกลางฝูงนกจริง ๆ นกบินห่างออกไป เสียงก็ไกลออกไป
/// ไม่ต้องต่ออะไร — particle เล่นเมื่อไหร่ เสียงมาเอง (เล่นซ้ำได้)
/// </summary>
[DisallowMultipleComponent]
public class FlockAudio : MonoBehaviour
{
    [Tooltip("ว่าง = ใช้ Particle System บน GameObject นี้")]
    [SerializeField] private ParticleSystem _particles;

    [Header("1. บินขึ้น")]
    [Tooltip("เสียงปีกกระพือพร้อมกันตอนฝูงบินขึ้น — ว่าง = ไม่มี")]
    [SerializeField] private SoundID _takeoff;

    [Header("2. การ้องตามหลัง")]
    [Tooltip("เสียงการ้อง 1 ครั้ง — ใส่หลายไฟล์ใน Clips ให้สุ่ม")]
    [SerializeField] private SoundID _caw;

    [Tooltip("ร้องกี่ครั้งหลังบินขึ้น")]
    [SerializeField, Min(0)] private int _cawCount = 3;

    [Tooltip("ครั้งแรกเร็วสุดกี่วินาทีหลังบินขึ้น")]
    [SerializeField, Min(0f)] private float _cawFirstDelay = 0.3f;

    [Tooltip("ร้องครั้งสุดท้ายไม่เกินกี่วินาทีหลังบินขึ้น")]
    [SerializeField, Min(0.1f)] private float _cawWindow = 3f;

    [Header("3. เสียงปีกระหว่างบิน (ลูป)")]
    [Tooltip("เสียงปีกตีลมแบบวน — ว่าง = ไม่มี")]
    [SerializeField] private SoundID _wingsLoop;

    [Tooltip("ค่อย ๆ ดังขึ้นตอนเริ่ม (วินาที)")]
    [SerializeField, Min(0f)] private float _wingsFadeIn = 0.2f;

    [Tooltip("นกหมดฝูงแล้วค่อย ๆ เงียบ (วินาที)")]
    [SerializeField, Min(0f)] private float _wingsFadeOut = 0.6f;

    [Header("ตามฝูง")]
    [Tooltip("ติ๊ก = เสียงลอยตามกลางฝูงนก / ไม่ติ๊ก = เสียงออกจากจุดที่วาง particle")]
    [SerializeField] private bool _followFlock = true;

    [Header("Debug")]
    [SerializeField] private bool _log;

    private const float FollowInterval = 0.1f;   // หากลางฝูงทุก 0.1 วิ พอแล้ว ไม่ต้องทุกเฟรม

    private Transform _anchor;
    private ParticleSystem.Particle[] _buffer;
    private bool _flying;
    private int _peakCount;
    private float _nextFollow;
    private readonly List<float> _cawTimes = new List<float>();
    private SFXHandle _wings = SFXHandle.None;

    private void Awake()
    {
        if (_particles == null) _particles = GetComponent<ParticleSystem>();
        if (_particles == null)
            Debug.LogWarning($"[FlockAudio] {name} ไม่มี Particle System — ลากใส่ช่อง Particles", this);

        // จุดที่เสียงเกาะ — ย้ายไปกลางฝูงเรื่อย ๆ
        _anchor = new GameObject("FlockSound").transform;
        _anchor.SetParent(transform, false);
    }

    private void OnDisable()
    {
        _wings.Stop();
        _wings = SFXHandle.None;
        _cawTimes.Clear();
        _flying = false;
    }

    private void Update()
    {
        if (_particles == null) return;

        int count = _particles.particleCount;

        if (!_flying && count > 0) Takeoff(count);
        if (!_flying) return;

        _peakCount = Mathf.Max(_peakCount, count);

        if (_followFlock && Time.time >= _nextFollow)
        {
            _nextFollow = Time.time + FollowInterval;
            MoveAnchorToFlock();
        }

        UpdateCaws();

        // นกค่อย ๆ หาย = ปีกค่อย ๆ เบา
        float level = _peakCount > 0 ? (float)count / _peakCount : 0f;
        if (_wings.IsValid) _wings.SetVolumeMultiplier(level);

        if (count == 0) Land();
    }

    // ---------- จังหวะ ----------

    private void Takeoff(int count)
    {
        _flying = true;
        _peakCount = count;
        _nextFollow = 0f;
        MoveAnchorToFlock();

        if (SoundManager.Instance == null) return;

        if (_takeoff != null)
            SoundManager.Instance.PlaySFXAttachedTracked(_takeoff, _anchor, out _);

        // สุ่มเวลาร้อง แล้วเรียงจากก่อนไปหลัง
        _cawTimes.Clear();
        for (int i = 0; i < _cawCount; i++)
            _cawTimes.Add(Time.time + Random.Range(_cawFirstDelay, Mathf.Max(_cawFirstDelay, _cawWindow)));
        _cawTimes.Sort();

        if (_wingsLoop != null)
        {
            _wings.Stop();
            _wings = SoundManager.Instance.PlayLoopSFXForeverAttached(_wingsLoop, _anchor);
            if (_wings.IsValid && _wingsFadeIn > 0f)
            {
                _wings.SetVolumeMultiplier(0f);
                _wings.FadeToVolumeMultiplier(1f, _wingsFadeIn);
            }
        }

        if (_log) Debug.Log($"[FlockAudio] {name} บินขึ้น ({count} ตัว)", this);
    }

    private void UpdateCaws()
    {
        while (_cawTimes.Count > 0 && Time.time >= _cawTimes[0])
        {
            _cawTimes.RemoveAt(0);
            if (_caw != null && SoundManager.Instance != null)
                SoundManager.Instance.PlaySFXAttachedTracked(_caw, _anchor, out _);
        }
    }

    private void Land()
    {
        _flying = false;
        _cawTimes.Clear();

        if (_wings.IsValid)
        {
            if (_wingsFadeOut > 0f) _wings.FadeOutAndStop(_wingsFadeOut);
            else _wings.Stop();
        }
        _wings = SFXHandle.None;

        if (_log) Debug.Log($"[FlockAudio] {name} ฝูงหายหมดแล้ว", this);
    }

    // ---------- หากลางฝูง ----------

    private void MoveAnchorToFlock()
    {
        if (!_followFlock || _particles == null)
        {
            _anchor.position = transform.position;
            return;
        }

        int max = _particles.main.maxParticles;
        if (_buffer == null || _buffer.Length < max) _buffer = new ParticleSystem.Particle[max];

        int n = _particles.GetParticles(_buffer);
        if (n == 0) return;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < n; i++) sum += _buffer[i].position;
        Vector3 center = sum / n;

        // ตำแหน่ง particle อยู่ใน space ที่ตั้งไว้ — แปลงเป็นโลกก่อน
        ParticleSystem.MainModule main = _particles.main;
        switch (main.simulationSpace)
        {
            case ParticleSystemSimulationSpace.Local:
                center = _particles.transform.TransformPoint(center);
                break;
            case ParticleSystemSimulationSpace.Custom:
                if (main.customSimulationSpace != null)
                    center = main.customSimulationSpace.TransformPoint(center);
                break;
        }

        _anchor.position = center;
    }
}
