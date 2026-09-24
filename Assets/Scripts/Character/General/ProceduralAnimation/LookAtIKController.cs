using UnityEngine;

[RequireComponent(typeof(Animator))]
public class LookAtIKController : MonoBehaviour
{
    [Header("ข้อจำกัดการมองเห็น (Limits)")]
    [Tooltip("ระยะไกลสุดที่ตัวละครจะเริ่มมองเป้าหมาย")]
    public float maxLookDistance = 10f;

    [Header("ระบบกันมองทะลุกำแพง (Line of Sight)")]
    [Tooltip("เลเยอร์ของกำแพงหรือสิ่งกีดขวาง")]
    public LayerMask obstacleMask;
    [Tooltip("จุดยิงแสงระดับสายตา (แกน Y แนะนำที่ 1.5)")]
    public Vector3 eyeOffset = new Vector3(0, 1.5f, 0);

    [Header("จุดที่มอง")]
    [Tooltip("ยกจุดมองขึ้นจากเป้ากี่เมตร\n" +
             "เป้าเป็นตัว Player (จุดอยู่ที่เท้า) → ใส่ Y ประมาณ 1.6 ให้มองที่หน้า\n" +
             "เป้าเป็นกล้อง → ใส่ 0")]
    public Vector3 targetOffset = Vector3.zero;

    [Header("ตั้งค่าความสมูท")]
    [Tooltip("น้ำหนักการมองสูงสุด (1 = มองเต็มที่)")]
    [Range(0f, 1f)] public float maxLookWeight = 1f;
    public float smoothSpeed = 5f;

    [Header("แบ่งการหมุนให้แต่ละส่วน")]
    [Tooltip("ตัวช่วยหมุนแค่ไหน — ยิ่งมาก คอยิ่งไม่ต้องบิดคนเดียว\nค่าเดิม 0.1 = คอรับเกือบหมด เลยบิดจนดูเหมือนคอหัก")]
    [Range(0f, 1f)] public float bodyWeight = 0.3f;

    [Tooltip("หัวหันแค่ไหน")]
    [Range(0f, 1f)] public float headWeight = 0.6f;

    [Tooltip("ตาหันแค่ไหน (มีผลเฉพาะโมเดลที่มีกระดูกตา)")]
    [Range(0f, 1f)] public float eyesWeight = 1f;

    [Tooltip("จำกัดองศา — 0 = หันได้ไม่จำกัด / 1 = หันไม่ได้เลย\nค่าเดิม 0.5 หันได้เกือบ 90° ต่อข้าง ยิ่งมากยิ่งหันได้น้อยลง")]
    [Range(0f, 1f)] public float clampWeight = 0.7f;

    [Header("🛠️ Debug Tools")]
    [Tooltip("เปิด-ปิด การแสดงเส้น Gizmos ในหน้า Scene")]
    public bool showGizmos = true; // ทำสวิตช์เปิด-ปิด Gizmos ตามที่ Lead สั่ง

    // เปลี่ยนตัวแปรพวกนี้ให้เป็น Private เพื่อบังคับให้ระบบอื่นเรียกใช้ผ่าน API เท่านั้น
    private Transform _targetToLookAt;
    private bool _isIKEnabled = false;

    private Animator _animator;
    private float _currentWeight = 0f;
    private float _targetWeight = 0f;

    // จุดที่มองจริง — ใช้ทั้งตอนเช็คกำแพงและตอนหันหัว จะได้ตรงกัน
    private Vector3 LookPoint => _targetToLookAt.position + targetOffset;

    private void Start()
    {
        _animator = GetComponent<Animator>();
    }

    #region Public API (สำหรับให้คลาสอื่นเรียกใช้งาน)

    /// <summary>
    /// ใช้สำหรับตั้งค่าเป้าหมายที่ต้องการให้ตัวละครหันไปมอง
    /// </summary>
    public void SetLookTarget(Transform newTarget)
    {
        _targetToLookAt = newTarget;
    }

    /// <summary>
    /// ใช้สำหรับเปิด-ปิด ระบบหันคอ (IK)
    /// </summary>
    public void SetIKEnabled(bool isEnabled)
    {
        _isIKEnabled = isEnabled;

        // ถ้าถูกสั่งปิด ให้เคลียร์น้ำหนักเป้าหมายเป็น 0 (เพื่อให้คอค่อยๆ หันกลับมาตรงๆ)
        if (!_isIKEnabled)
        {
            _targetWeight = 0f;
        }
    }

    #endregion

    private void Update()
    {
        // ถ้าโดนสั่งปิด IK ก็ไม่ต้องเปลืองแรงคำนวณระยะทาง
        if (!_isIKEnabled) return;

        if (_targetToLookAt != null)
        {
            float distance = Vector3.Distance(transform.position, _targetToLookAt.position);

            bool isBlocked = false;
            Vector3 startPos = transform.position + eyeOffset;

            if (Physics.Linecast(startPos, LookPoint, out RaycastHit hit, obstacleMask))
            {
                isBlocked = true;
            }

            if (distance <= maxLookDistance && !isBlocked)
            {
                _targetWeight = maxLookWeight;
            }
            else
            {
                _targetWeight = 0f;
            }
        }
        else
        {
            _targetWeight = 0f;
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null) return;

        _currentWeight = Mathf.Lerp(_currentWeight, _targetWeight, Time.deltaTime * smoothSpeed);

        if (_currentWeight > 0.01f && _targetToLookAt != null)
        {
            _animator.SetLookAtWeight(_currentWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
            _animator.SetLookAtPosition(LookPoint);
        }
        else
        {
            _animator.SetLookAtWeight(_currentWeight);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // ถ้าสวิตช์ปิดอยู่ ให้หยุดวาดเส้นทันที
        if (!showGizmos) return;

        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, maxLookDistance);

        if (_targetToLookAt != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + eyeOffset, LookPoint);
        }
    }
}