using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlorySoundPlayer : MonoBehaviour
{
    [System.Serializable]
    private class TimedSound
    {
        [Tooltip("ดังตอนท่าเล่นไปกี่วินาที นับจากตอนท่าเริ่ม")]
        [Min(0f)] public float Seconds;
        public SoundID Sound;
    }

    [System.Serializable]
    private class AnimationSound
    {
        [Tooltip("ท่า Glory — ใส่ท่าฝั่งผู้เล่นหรือฝั่งผีก็ได้ (เช่น Victim Glory Tanee 2)")]
        public PairedAnimationId Animation;
        public SoundID Sound;
    }

    [Tooltip("เสียงเดียวยาวตลอดท่า ดังตอนท่า Glory เริ่มเล่น")]
    [SerializeField] private SoundID _sound;

    [Header("เสียงแยกตามท่า (ผีที่มี Glory หลายท่า เช่น ตานีครั้งสุดท้าย)")]
    [Tooltip("ท่าที่เล่นตรงกับในรายการ = ใช้เสียงของท่านั้นแทน\nไม่ตรง = ใช้ช่อง Sound / Timed Sounds ตามปกติ")]
    [SerializeField] private List<AnimationSound> _perAnimation = new List<AnimationSound>();

    [Header("แยกหลายจังหวะ (ไม่ใช้ก็ไม่ต้องติ๊ก)")]
    [Tooltip("ติ๊ก = ใช้รายการ Timed Sounds แทนช่อง Sound\nแต่ละเสียงดังตามวินาทีที่ตั้ง นับจากตอนท่าเริ่ม")]
    [SerializeField] private bool _useTimedSounds;
    [SerializeField] private List<TimedSound> _timedSounds = new List<TimedSound>();

    [Header("Debug")]
    [Tooltip("ขึ้น log ตอนจับได้ว่าท่า Glory เริ่ม")]
    [SerializeField] private bool _logPlay;

    private PairedAnimationActor _actor;
    private bool _wasPlaying;

    private void Awake()
    {
        // วางบนตัวผีหรือตัวลูกก็ได้ หา PairedAnimationActor ของผีให้เอง
        _actor = GetComponentInParent<PairedAnimationActor>();
        if (_actor == null)
            _actor = GetComponentInChildren<PairedAnimationActor>();

        if (_actor == null)
            Debug.LogWarning("[GlorySound] หา PairedAnimationActor ของผีไม่เจอ — วางสคริปต์นี้บนตัวผีหรือตัวลูกของผี", this);
    }

    private void OnEnable() => _wasPlaying = false;

    private void Update()
    {
        bool playing = IsGloryPlaying();
        if (playing && !_wasPlaying)
            Play();
        _wasPlaying = playing;
    }

    // นับตอนท่าเล่นจริงเท่านั้น ไม่นับช่วงดูดตัวเข้าที่ / รอกล้อง เสียงจะได้ตรงกับท่า
    private bool IsGloryPlaying()
    {
        if (_actor == null)
            return false;

        PairedRequest request = _actor.CurrentRequest;
        return request.Animation != null && request.Type == PairedActionType.Glory &&
               !_actor.IsPreparing && !_actor.IsFinished;
    }

    private void Play()
    {
        SoundID special = FindAnimationSound();
        if (special != null)
        {
            if (_logPlay)
                Debug.Log($"[GlorySound] ท่า Glory เริ่ม — ท่าพิเศษ {_actor.CurrentRequest.Animation.name} → {special.name}", this);
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlaySFXAttached(special, transform);
            return;
        }

        if (_logPlay)
            Debug.Log($"[GlorySound] ท่า Glory เริ่ม — {(_useTimedSounds ? $"แบบหลายจังหวะ ({_timedSounds.Count} เสียง)" : (_sound != null ? _sound.name : "ยังไม่ได้ใส่เสียง"))}", this);

        // ฉากที่ไม่มี SoundManager (ฉากเทสของเพื่อน) จะเงียบเฉยๆ ไม่พัง
        if (SoundManager.Instance == null)
            return;

        if (!_useTimedSounds)
        {
            if (_sound != null)
                SoundManager.Instance.PlaySFXAttached(_sound, transform);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(PlayTimed());
    }

    // ท่าที่กำลังเล่น (ฝั่งผี หรือฝั่งผู้เล่น) ตรงกับรายการแยกตามท่าไหม
    private SoundID FindAnimationSound()
    {
        if (_actor == null || _perAnimation == null)
            return null;

        PairedRequest request = _actor.CurrentRequest;
        foreach (AnimationSound entry in _perAnimation)
        {
            if (entry == null || entry.Animation == null || entry.Sound == null)
                continue;
            if (entry.Animation == request.Animation || entry.Animation == request.PartnerAnimation)
                return entry.Sound;
        }
        return null;
    }

    private IEnumerator PlayTimed()
    {
        var queue = new List<TimedSound>();
        foreach (TimedSound entry in _timedSounds)
            if (entry != null && entry.Sound != null)
                queue.Add(entry);
        queue.Sort((a, b) => a.Seconds.CompareTo(b.Seconds));

        float elapsed = 0f;
        foreach (TimedSound entry in queue)
        {
            while (elapsed < entry.Seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (SoundManager.Instance != null)
                SoundManager.Instance.PlaySFXAttached(entry.Sound, transform);
        }
    }
}
