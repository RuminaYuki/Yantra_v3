using UnityEngine;

public class PlaySoundOnEventChannel : MonoBehaviour
{
    [Header("ฟังช่องไหน")]
    [Tooltip("ลาก VoidEventChannelSO ตัวเดียวกับที่ระบบนั้นใช้\n" +
        "เช่นเสียงปืน = ช่องเดียวกับ Handle Spawn Event ใน GunController")]
    [SerializeField] private VoidEventChannelSO _channel;

    [Header("เสียง")]
    [SerializeField] private SoundID _sound;

    [Header("Options")]
    [Tooltip("จุดกำเนิดเสียง เว้นว่างจะใช้ตำแหน่งของ GameObject นี้\n" +
        "เสียงปืนควรใส่ปลายกระบอก จะได้ทิศทางถูก")]
    [SerializeField] private Transform _soundOrigin;

    [Tooltip("ให้เสียงวิ่งตามจุดกำเนิด\nเปิดถ้าตัวละครยังขยับระหว่างเสียงยังดังอยู่")]
    [SerializeField] private bool _followOrigin = true;

    [Tooltip("เว้นอย่างน้อยกี่วินาทีก่อนเล่นซ้ำ\n0 = ไม่กัน เล่นทุกครั้งที่ event ยิง")]
    [SerializeField] private float _minInterval = 0f;

    [Header("Debug")]
    [SerializeField] private bool _logPlays = false;

    private float _lastPlayTime = -999f;

    private void OnEnable()
    {
        _lastPlayTime = -999f;

        if (_channel != null) _channel.Raised += HandleRaised;

#if UNITY_EDITOR
        if (_channel == null)
            Debug.LogWarning("[EventSound] ยังไม่ได้ลาก Event Channel มาใส่ — จะไม่มีเสียงเลย", this);
#endif
    }

    /// <summary>
    /// ถอดออกเสมอ
    ///
    /// โปรเจกต์เราปิด Domain Reload ไว้ event ใน ScriptableObject จึงไม่ถูกล้างตอนกด Stop
    /// ถ้าไม่ถอด กด Play รอบสองจะมีตัวสมัครค้างจากรอบแรก ยิงทีเดียวได้ยินเสียง 2 นัด
    /// </summary>
    private void OnDisable()
    {
        if (_channel != null) _channel.Raised -= HandleRaised;
    }

    private void HandleRaised()
    {
        if (_minInterval > 0f && Time.time - _lastPlayTime < _minInterval) return;
        _lastPlayTime = Time.time;

        if (_logPlays)
            Debug.Log($"[EventSound] {_channel.name} → {(_sound != null ? _sound.name : "ยังไม่ได้ใส่เสียง")}", this);

        if (_sound == null || SoundManager.Instance == null) return;

        Transform origin = _soundOrigin != null ? _soundOrigin : transform;

        if (_followOrigin)
            SoundManager.Instance.PlaySFXAttached(_sound, origin);
        else
            SoundManager.Instance.PlaySFX(_sound, origin.position);
    }
}