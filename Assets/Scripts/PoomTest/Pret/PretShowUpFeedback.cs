using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

// วางบนตัวเปรตแต่ละตัว — ฟัง Flag "โผล่" ของเปรตตัวนี้เอง (StateFlags.FlagChanged) แล้ว
// หันกล้องไปมอง + จอสั่นครืน ๆ + ท้องฟ้าค่อย ๆ แดง + เรียก On Show Up (นกบิน / เสียง)
// พอ Flag กลับเป็น false (เปรตกลับบ้าน) ท้องฟ้าค่อย ๆ กลับปกติ
//
// ท้องฟ้าเป็นของทั้งฉาก ไม่ใช่ของเปรตตัวไหน: แดงเมื่อมีเปรตโผล่อยู่อย่างน้อย 1 ตัว
// และกลับปกติเมื่อทุกตัวกลับบ้านแล้ว (นับรวมทุกตัว)
public class PretShowUpFeedback : MonoBehaviour
{
    [Header("สัญญาณ")]
    [Tooltip("Flag ที่ State ของเปรตตั้ง: true ตอนโผล่ (ShowUp) / false ตอนกลับบ้าน")]
    [SerializeField] private FlagSO _showUpFlag;

    [Tooltip("StateFlags ของเปรตตัวนี้ — ว่าง = หาบนตัวนี้หรือตัวแม่ให้เอง")]
    [SerializeField] private StateFlagsAccess _flags;

    [Header("หันไปมอง")]
    [Tooltip("จุดที่จะให้กล้องหันไปมอง — ว่าง = ตัวเปรตนี้เอง")]
    [SerializeField] private Transform _lookTarget;

    [Tooltip("ยกจุดที่มองขึ้นจากเท้าเปรตกี่เมตร — ใส่ประมาณความสูงหัวมัน")]
    [SerializeField] private float _lookHeight = 1.8f;

    [Tooltip("บังคับมองค้างไว้กี่วินาที แล้วค่อยคืนเมาส์ให้ผู้เล่น — 0 = ไม่หัน")]
    [SerializeField, Min(0f)] private float _lookDuration = 2.5f;

    [Tooltip("ใช้เวลาหันกี่วินาที — น้อย = หันพรวด / มาก = ค่อย ๆ หัน")]
    [SerializeField, Min(0.01f)] private float _lookTurnTime = 0.35f;

    [Header("จอสั่นครืน ๆ")]
    [Tooltip("แรงสั่นที่ค้างไว้ — 0 = ไม่สั่น / 1 = แรงสุด")]
    [SerializeField, Range(0f, 1f)] private float _rumbleStrength = 0.3f;

    [Tooltip("สั่นนานกี่วินาที")]
    [SerializeField, Min(0f)] private float _rumbleDuration = 2f;

    [Tooltip("ช่วงท้ายค่อย ๆ เบาลงกี่วินาที")]
    [SerializeField, Min(0f)] private float _rumbleFadeOut = 0.6f;

    [Header("ท้องฟ้าแดง")]
    [Tooltip("Global Volume โทนแดง (Priority สูงกว่าตัวหลักของฉาก) — เปรตทุกตัวใส่ Volume ตัวเดียวกันได้\n" +
             "เริ่มเกมจะตั้ง Weight = 0 ให้เอง")]
    [SerializeField] private Volume _redSkyVolume;

    [Tooltip("ค่อย ๆ แดงจนเต็มในกี่วินาที")]
    [SerializeField, Min(0f)] private float _redInTime = 4f;

    [Tooltip("ค่อย ๆ กลับปกติในกี่วินาที")]
    [SerializeField, Min(0f)] private float _redOutTime = 6f;

    [Header("ตอนโผล่")]
    [Tooltip("ลากของที่ต้องเล่นตอนเปรตโผล่มาใส่ เช่น Particle นกบิน → Play()")]
    public UnityEvent OnShowUp;

    // ---------- ใช้ร่วมกันทุกเปรต ----------

    // เปรตที่โผล่อยู่ตอนนี้กี่ตัว — มากกว่า 0 = ท้องฟ้าแดง
    private static int s_showingCount;

    // เฟรมล่าสุดที่ขยับท้องฟ้าไปแล้ว — หลายตัวใช้ Volume เดียวกัน ให้ขยับแค่ครั้งเดียวต่อเฟรม ไม่งั้นแดงเร็วเกิน
    private static int s_redSkyFrame = -1;

    // ปิด Domain Reload ใน Editor ค่า static จะค้างข้ามการกด Play — ล้างทุกครั้งที่เริ่มเกม
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetShared()
    {
        s_showingCount = 0;
        s_redSkyFrame = -1;
    }

    // ---------- ของตัวนี้ ----------

    private PlayerCameraController _camera;
    private float _rumbleTime = -1f;

    // ตัวนี้นับรวมอยู่ใน s_showingCount หรือยัง — กันนับซ้ำ / ลืมลบ
    private bool _isShowing;

    private void Awake()
    {
        if (_flags == null)
            _flags = GetComponentInParent<StateFlagsAccess>();

        if (_lookTarget == null)
            _lookTarget = transform;

        if (_redSkyVolume == null)
        {
            GameObject volumeObject = GameObject.FindWithTag("RedSkyVolume");
            if (volumeObject != null)
                _redSkyVolume = volumeObject.GetComponent<Volume>();
        }
        
        if (_redSkyVolume != null)
            _redSkyVolume.weight = 0f;
    }

    private void OnEnable()
    {
        if (_flags == null || _showUpFlag == null)
        {
            Debug.LogWarning("[PretShowUpFeedback] ยังไม่ได้ใส่ Show Up Flag หรือหา StateFlags ของเปรตไม่เจอ", this);
            return;
        }

        _flags.FlagChanged += HandleFlagChanged;
    }

    private void OnDisable()
    {
        if (_flags != null)
            _flags.FlagChanged -= HandleFlagChanged;

        _rumbleTime = -1f;

        // ตัวนี้หายไปกลางทาง (ปิด / ถูกทำลาย) — ไม่นับว่าโผล่อยู่แล้ว ไม่งั้นท้องฟ้าแดงค้าง
        SetShowing(false);
    }

    private void HandleFlagChanged(FlagSO flag, bool value)
    {
        if (flag != _showUpFlag)
            return;

        if (value && !_isShowing)
        {
            SetShowing(true);
            LookAtPret();
            _rumbleTime = 0f;
            OnShowUp?.Invoke();
        }
        else if (!value)
        {
            SetShowing(false);
        }
    }

    private void SetShowing(bool showing)
    {
        if (_isShowing == showing)
            return;

        _isShowing = showing;
        s_showingCount = Mathf.Max(0, s_showingCount + (showing ? 1 : -1));
    }

    private void LookAtPret()
    {
        if (_lookTarget == null || _lookDuration <= 0f)
            return;

        if (_camera == null)
            _camera = FindFirstObjectByType<PlayerCameraController>();

        if (_camera != null)
            _camera.StartForcedLook(_lookTarget, Vector3.up * _lookHeight, _lookDuration, _lookTurnTime);
    }

    private void Update()
    {
        UpdateRedSky();

        if (Time.timeScale > 0f)
            UpdateRumble();
    }

    private void UpdateRedSky()
    {
        if (_redSkyVolume == null || s_redSkyFrame == Time.frameCount)
            return;

        s_redSkyFrame = Time.frameCount;

        float target = s_showingCount > 0 ? 1f : 0f;
        if (Mathf.Approximately(_redSkyVolume.weight, target))
            return;

        float time = target > _redSkyVolume.weight ? _redInTime : _redOutTime;
        _redSkyVolume.weight = time > 0f
            ? Mathf.MoveTowards(_redSkyVolume.weight, target, Time.deltaTime / time)
            : target;
    }

    // เติมให้สั่นค้างไว้ระดับเดิมจนหมดเวลา
    private void UpdateRumble()
    {
        if (_rumbleTime < 0f)
            return;

        _rumbleTime += Time.deltaTime;
        if (_rumbleTime >= _rumbleDuration)
        {
            _rumbleTime = -1f;
            return;
        }

        float timeLeft = _rumbleDuration - _rumbleTime;
        float strength = _rumbleFadeOut > 0f
            ? _rumbleStrength * Mathf.Clamp01(timeLeft / _rumbleFadeOut)
            : _rumbleStrength;

        CameraShaker shaker = CameraShaker.Instance;
        if (shaker != null && shaker.Trauma < strength)
            shaker.AddTrauma(strength - shaker.Trauma);
    }
}
