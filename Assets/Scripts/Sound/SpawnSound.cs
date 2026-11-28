using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เสียงของชิ้นที่เกิดขึ้นมาทีละหลายอัน (หนามตานี ดินแตก ลูกน้องที่ถูกเรียก)
/// - On Spawn: ดังตอนชิ้นนี้ถูกเปิดขึ้นมา (สร้างใหม่ / หยิบจาก pool / SplineItemCache โชว์)
/// - On State: ดังตอน Animator ของชิ้นนี้เข้า state ที่ตั้ง (เช่น หนามหัก Tanee_Thorn_Finish)
/// กันเสียงล้น: ของเกิดพร้อมกันหลายสิบอัน เสียงเดียวกันดังได้ไม่เกิน Max Per Burst ครั้งในช่วง Burst Window
/// </summary>
[DisallowMultipleComponent]
public class SpawnSound : MonoBehaviour
{
    [Header("ตอนเกิด")]
    [Tooltip("ดังตอนชิ้นนี้ถูกเปิดขึ้นมา — ว่าง = ไม่มีเสียง")]
    [SerializeField] private SoundID _onSpawn;

    [Tooltip("เกิดแล้วรอกี่วินาทีค่อยดัง")]
    [SerializeField, Min(0f)] private float _spawnDelay;

    [Header("ตอน Animator เข้า state")]
    [Tooltip("ชื่อ state ใน Animator ของชิ้นนี้ (หรือตัวลูก) — ว่าง = ไม่ใช้")]
    [SerializeField] private string _state = "";

    [SerializeField] private SoundID _onState;

    [Header("กันเสียงล้น (ของเกิดพร้อมกันหลายอัน)")]
    [Tooltip("เสียงเดียวกันดังได้กี่ครั้งในช่วง Burst Window — ที่เหลือเงียบ")]
    [SerializeField, Min(1)] private int _maxPerBurst = 3;

    [Tooltip("ช่วงเวลานับว่าเกิดพร้อมกัน (วินาที)")]
    [SerializeField, Min(0.01f)] private float _burstWindow = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool _logPlay;

    // ฉากเพิ่งโหลด — pool สร้างของแล้วปิดทันที ไม่ต้องมีเสียง
    private const float IgnoreAfterLoad = 1f;

    private static readonly Dictionary<SoundID, float> _burstStart = new Dictionary<SoundID, float>();
    private static readonly Dictionary<SoundID, int> _burstCount = new Dictionary<SoundID, int>();

    private Animator _animator;
    private int _stateHash;
    private bool _inState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _burstStart.Clear();
        _burstCount.Clear();
    }

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>(true);
        _stateHash = string.IsNullOrEmpty(_state) ? 0 : Animator.StringToHash(_state);
    }

    private void OnEnable()
    {
        _inState = false;
        if (_onSpawn == null || Time.timeSinceLevelLoad < IgnoreAfterLoad) return;

        if (_spawnDelay > 0f) StartCoroutine(PlayLater(_onSpawn, _spawnDelay));
        else TryPlay(_onSpawn);
    }

    private IEnumerator PlayLater(SoundID id, float delay)
    {
        yield return new WaitForSeconds(delay);
        TryPlay(id);
    }

    private void Update()
    {
        if (_stateHash == 0 || _onState == null || _animator == null) return;
        if (!_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null) return;

        bool now = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0).shortNameHash == _stateHash
            : _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _stateHash;

        if (now && !_inState) TryPlay(_onState);
        _inState = now;
    }

    private void TryPlay(SoundID id)
    {
        if (id == null || SoundManager.Instance == null) return;

        float now = Time.time;
        if (!_burstStart.TryGetValue(id, out float start) || now - start > _burstWindow)
        {
            _burstStart[id] = now;
            _burstCount[id] = 0;
        }
        if (_burstCount[id] >= _maxPerBurst) return;
        _burstCount[id]++;

        SoundManager.Instance.PlaySFX(id, transform.position);
        if (_logPlay) Debug.Log($"[SpawnSound] {name} → {id.name}", this);
    }
}
