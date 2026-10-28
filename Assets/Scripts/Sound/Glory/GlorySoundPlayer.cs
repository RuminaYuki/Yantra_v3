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

    [Tooltip("เสียงเดียวยาวตลอดท่า ดังตอนท่า Glory เริ่มเล่น")]
    [SerializeField] private SoundID _sound;

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
