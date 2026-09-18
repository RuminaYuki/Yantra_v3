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

    [Header("ตั้งค่าความสมูท")]
    [Tooltip("น้ำหนักการมองสูงสุด (1 = มองเต็มที่)")]
    [Range(0f, 1f)] public float maxLookWeight = 1f;
    public float smoothSpeed = 5f;

    [Header("🛠️ Debug Tools")]
    [Tooltip("เปิด-ปิด การแสดงเส้น Gizmos ในหน้า Scene")]
    public bool showGizmos = true; // ทำสวิตช์เปิด-ปิด Gizmos ตามที่ Lead สั่ง

    // เปลี่ยนตัวแปรพวกนี้ให้เป็น Private เพื่อบังคับให้ระบบอื่นเรียกใช้ผ่าน API เท่านั้น
    private Transform _targetToLookAt;
    private bool _isIKEnabled = false;

    private Animator _animator;
    private float _currentWeight = 0f;
    private float _targetWeight = 0f;

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

            if (Physics.Linecast(startPos, _targetToLookAt.position, out RaycastHit hit, obstacleMask))
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
            _animator.SetLookAtWeight(_currentWeight, 0.1f, 0.8f, 1.0f, 0.5f);
            _animator.SetLookAtPosition(_targetToLookAt.position);
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
            Gizmos.DrawLine(transform.position + eyeOffset, _targetToLookAt.position);
        }
    }
}