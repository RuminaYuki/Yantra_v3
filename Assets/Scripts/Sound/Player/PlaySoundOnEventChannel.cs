using UnityEngine;

public class PlaySoundOnEventChannel : MonoBehaviour
{
    /// <summary>
    /// ช่อง Float ยิงทุกครั้งที่ค่าเปลี่ยน แต่เรามักอยากได้แค่บางทิศทาง
    /// เช่นหลอดเกราะ — อยากได้เสียงตอนโดนตี (ลด) ไม่ใช่ตอนฟื้น (เพิ่ม)
    /// </summary>
    public enum FloatCondition
    {
        /// <summary>ยิงทุกครั้งที่ค่าเปลี่ยน</summary>
        Always,

        /// <summary>เฉพาะตอนค่าลดลง — บล็อกโดน / เลือดลด</summary>
        WhenDecreased,

        /// <summary>เฉพาะตอนค่าเพิ่มขึ้น — ฟื้นเกราะ / ได้เลือดคืน</summary>
        WhenIncreased,

        /// <summary>เฉพาะตอนค่า ≤ Threshold — เตือนใกล้ตาย</summary>
        WhenAtOrBelowThreshold,

        /// <summary>เฉพาะตอนค่า ≥ Threshold — เต็มแล้วพร้อมใช้</summary>
        WhenAtOrAboveThreshold
    }

    [Header("ฟังช่องไหน (ใส่ช่องเดียวหรือทั้งคู่ก็ได้)")]
    [Tooltip("ช่องแบบไม่มีค่า เช่น PlayerOnGuardPointDepleted_VEC")]
    [SerializeField] private VoidEventChannelSO _voidChannel;

    [Tooltip("ช่องแบบส่งตัวเลขมาด้วย เช่น PlayerOnCurrentGuardPointChange_Fl")]
    [SerializeField] private FloatEventChannelSO _floatChannel;

    [Header("กรองค่า (ใช้เฉพาะช่อง Float)")]
    [Tooltip("บล็อกโดน → เลือก WhenDecreased เพราะเกราะลดตอนโดนตี\n" +
        "ครั้งแรกที่ยิงจะยังไม่มีค่าเก่าให้เทียบ ระบบจะข้ามให้เอง ไม่ดังมั่ว")]
    [SerializeField] private FloatCondition _floatCondition = FloatCondition.Always;

    [Tooltip("ใช้เฉพาะโหมด AtOrBelow / AtOrAbove")]
    [SerializeField] private float _threshold = 0f;

    [Header("เสียง")]
    [SerializeField] private SoundID _sound;

    [Header("ตำแหน่ง")]
    [Tooltip("เว้นว่าง = ใช้ตำแหน่งของ GameObject นี้")]
    [SerializeField] private Transform _soundOrigin;

    [Tooltip("ให้เสียงวิ่งตามจุดกำเนิด เปิดไว้ถ้าตัวละครยังขยับระหว่างเสียงดัง")]
    [SerializeField] private bool _followOrigin = true;

    [Header("Options")]
    [Tooltip("เว้นอย่างน้อยกี่วินาทีก่อนเล่นซ้ำ\n\n" +
        "⚠️ ช่องโล่แตกต้องใส่อย่างน้อย 2 วินาที\n" +
        "เพราะ BlockSystem ยิง Raise() ทุกเฟรมตอนเกราะหมดแล้วยังกดบล็อกค้าง\n" +
        "ถ้าไม่กัน เสียงจะดังรัว 60 ครั้งต่อวินาที")]
    [SerializeField] private float _minInterval = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool _logPlays = false;

    private float _lastPlayTime = -999f;
    private float _lastValue;
    private bool _hasLastValue;

    private void OnEnable()
    {
        _lastPlayTime = -999f;
        _hasLastValue = false;

        if (_voidChannel != null) _voidChannel.Raised += HandleVoid;
        if (_floatChannel != null) _floatChannel.Raised += HandleFloat;

#if UNITY_EDITOR
        if (_voidChannel == null && _floatChannel == null)
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
        if (_voidChannel != null) _voidChannel.Raised -= HandleVoid;
        if (_floatChannel != null) _floatChannel.Raised -= HandleFloat;
    }

    private void HandleVoid() => Fire("void");

    private void HandleFloat(float value)
    {
        bool passed = PassesCondition(value);

        _lastValue = value;
        _hasLastValue = true;

        if (passed) Fire(value.ToString("0.##"));
    }

    private bool PassesCondition(float value)
    {
        switch (_floatCondition)
        {
            case FloatCondition.WhenDecreased:
                // ครั้งแรกไม่มีค่าเก่าให้เทียบ ข้ามไปก่อนดีกว่าเดามั่ว
                return _hasLastValue && value < _lastValue;

            case FloatCondition.WhenIncreased:
                return _hasLastValue && value > _lastValue;

            case FloatCondition.WhenAtOrBelowThreshold:
                return value <= _threshold;

            case FloatCondition.WhenAtOrAboveThreshold:
                return value >= _threshold;

            default:
                return true;
        }
    }

    private void Fire(string debugValue)
    {
        if (_minInterval > 0f && Time.time - _lastPlayTime < _minInterval) return;
        _lastPlayTime = Time.time;

        if (_logPlays)
            Debug.Log($"[EventSound] {debugValue} → {(_sound != null ? _sound.name : "ยังไม่ได้ใส่เสียง")}", this);

        if (_sound == null) return;

        // ฉากที่ไม่มี SoundManager (เช่นฉากเทสของเพื่อน) จะเงียบเฉยๆ ไม่พัง
        if (SoundManager.Instance == null) return;

        Transform origin = _soundOrigin != null ? _soundOrigin : transform;

        if (_followOrigin)
            SoundManager.Instance.PlaySFXAttached(_sound, origin);
        else
            SoundManager.Instance.PlaySFX(_sound, origin.position);
    }
}