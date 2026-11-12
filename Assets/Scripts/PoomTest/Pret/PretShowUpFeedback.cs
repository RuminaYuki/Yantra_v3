using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

// วางที่ไหนในซีนก็ได้ — ฟังสัญญาณเปรตโผล่ (State ShowUp ส่งมา) แล้ว
// หันกล้องไปมอง + จอสั่นครืน ๆ + ท้องฟ้าค่อย ๆ แดง + เรียก On Show Up (นกบิน / เสียง)
// พอได้สัญญาณเปรตกลับบ้าน ท้องฟ้าค่อย ๆ กลับปกติ
public class PretShowUpFeedback : MonoBehaviour
{
    [Header("สัญญาณ")]
    [Tooltip("สัญญาณที่ State ShowUp ของเปรตส่ง (PretShowUp_VoidEventChannel)")]
    [SerializeField] private VoidEventChannelSO _showUpChannel;

    [Tooltip("สัญญาณตอนเปรตเดินกลับบ้าน — ท้องฟ้าค่อย ๆ กลับปกติ (ว่างได้ = แดงค้าง)")]
    [SerializeField] private VoidEventChannelSO _backHomeChannel;

    [Header("หันไปมอง")]
    [Tooltip("ตัวเปรตที่จะให้กล้องหันไปมอง (เช่น Test Pret) — ว่าง = ไม่หัน")]
    [SerializeField] private Transform _lookTarget;

    [Tooltip("ยกจุดที่มองขึ้นจากเท้าเปรตกี่เมตร — ใส่ประมาณความสูงหัวมัน")]
    [SerializeField] private float _lookHeight = 1.8f;

    [Tooltip("บังคับมองค้างไว้กี่วินาที แล้วค่อยคืนเมาส์ให้ผู้เล่น")]
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
    [Tooltip("Global Volume โทนแดง (Priority สูงกว่าตัวหลักของฉาก) — เริ่มเกมจะตั้ง Weight = 0 ให้เอง")]
    [SerializeField] private Volume _redSkyVolume;

    [Tooltip("ค่อย ๆ แดงจนเต็มในกี่วินาที")]
    [SerializeField, Min(0f)] private float _redInTime = 4f;

    [Tooltip("ค่อย ๆ กลับปกติในกี่วินาที")]
    [SerializeField, Min(0f)] private float _redOutTime = 6f;

    [Header("ตอนโผล่")]
    [Tooltip("ลากของที่ต้องเล่นตอนเปรตโผล่มาใส่ เช่น Particle นกบิน → Play()")]
    public UnityEvent OnShowUp;

    private PlayerCameraController _camera;
    private float _rumbleTime = -1f;
    private float _redTarget;

    private void Awake()
    {
        // ตอนจูนสีใน Editor อาจเปิด Weight ค้างไว้ — เริ่มเกมให้ปิดเสมอ
        if (_redSkyVolume != null)
            _redSkyVolume.weight = 0f;
    }

    private void OnEnable()
    {
        if (_showUpChannel != null)
            _showUpChannel.Raised += HandleShowUp;
        else
            Debug.LogWarning("[PretShowUpFeedback] ยังไม่ได้ใส่ Show Up Channel", this);

        if (_backHomeChannel != null)
            _backHomeChannel.Raised += HandleBackHome;
    }

    private void OnDisable()
    {
        if (_showUpChannel != null)
            _showUpChannel.Raised -= HandleShowUp;
        if (_backHomeChannel != null)
            _backHomeChannel.Raised -= HandleBackHome;
        _rumbleTime = -1f;
    }

    private void HandleShowUp()
    {
        LookAtPret();
        _rumbleTime = 0f;
        _redTarget = 1f;
        OnShowUp?.Invoke();
    }

    private void HandleBackHome()
    {
        _redTarget = 0f;
    }

    private void LookAtPret()
    {
        if (_lookTarget == null)
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
        if (_redSkyVolume == null || Mathf.Approximately(_redSkyVolume.weight, _redTarget))
            return;

        float time = _redTarget > _redSkyVolume.weight ? _redInTime : _redOutTime;
        _redSkyVolume.weight = time > 0f
            ? Mathf.MoveTowards(_redSkyVolume.weight, _redTarget, Time.deltaTime / time)
            : _redTarget;
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