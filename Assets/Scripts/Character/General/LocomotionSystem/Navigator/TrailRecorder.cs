using System.Collections.Generic;
using UnityEngine;

// วางบน Nak: จดรอยเท้าที่เดินผ่าน แล้ววาง TrailPoint ไว้บนรอยเท้าย้อนหลังตามระยะ _followDistance
// Player ใช้ Follow3D ตาม TrailPoint (offset = 0, maintainOffset = off)
// ทำใน Update หลัง BaseLocomotion เดินเสร็จ (order 50 > 0) และก่อนรอบ animation
// ให้ Hand IK_target ที่เป็นลูกของ TrailPoint อยู่ที่ใหม่ก่อน IK คำนวณ (ไม่งั้นมือจะตามหลัง 1 เฟรม)
// Follow3D ของ Player ยังอ่านใน LateUpdate ได้ตามเดิม
[DefaultExecutionOrder(50)]
public class TrailRecorder : MonoBehaviour
{
    [SerializeField] private Transform _trailPoint;
    [SerializeField, Min(0.1f)] private float _followDistance = 1.2f;
    [Tooltip("เดินห่างจากจุดล่าสุดเท่านี้ถึงจะจดจุดใหม่")]
    [SerializeField, Min(0.01f)] private float _recordSpacing = 0.1f;

    // [0] = เก่าสุด, ตัวท้าย = ใหม่สุด
    private readonly List<Vector3> _points = new();

    public Transform TrailPoint => _trailPoint;

    // เรียกตอนเริ่ม Grab: รอยเท้าเส้นแรก = เส้นตรงจาก Player ถึง Nak
    // ไม่งั้นเฟรมแรก TrailPoint จะไปอยู่ที่รอยเท้าเก่าของ Nak แล้ว Player จะวาร์ปไปที่นั่น
    public void ResetTrail(Vector3 tailPosition)
    {
        _points.Clear();
        _points.Add(tailPosition);
        _points.Add(transform.position);
        UpdateTrailPoint();
    }

    private void Update()
    {
        Record();
        UpdateTrailPoint();
        Trim();
    }

    private void Record()
    {
        Vector3 current = transform.position;
        if (_points.Count == 0
            || (current - _points[^1]).sqrMagnitude >= _recordSpacing * _recordSpacing)
        {
            _points.Add(current);
        }
    }

    private void UpdateTrailPoint()
    {
        if (_trailPoint == null || _points.Count == 0) return;

        Vector3 position = SampleBack(_followDistance);
        _trailPoint.position = position;

        // หันหน้าไปหา Nak (โดนจูงมือ)
        Vector3 toLeader = transform.position - position;
        toLeader.y = 0f;
        if (toLeader.sqrMagnitude > 0.0001f)
            _trailPoint.rotation = Quaternion.LookRotation(toLeader, Vector3.up);
    }

    // เดินย้อนจากตัว Nak ไปตามรอยเท้าทีละช่วง จนครบระยะ distance
    private Vector3 SampleBack(float distance)
    {
        Vector3 from = transform.position;

        for (int i = _points.Count - 1; i >= 0; i--)
        {
            if (distance <= 0f) return from;

            Vector3 to = _points[i];
            float segment = Vector3.Distance(from, to);

            if (segment > 0f && segment >= distance)
                return Vector3.Lerp(from, to, distance / segment); // ระยะที่ต้องการอยู่ในช่วงนี้

            distance -= segment;
            from = to;
        }

        return _points[0]; // รอยเท้ายังสั้นกว่าระยะ ให้อยู่ที่ปลายสุดไปก่อน
    }

    // ลบรอยเท้าที่เก่าเกินระยะที่ต้องใช้ ไม่ให้ list โตไปเรื่อยๆ
    private void Trim()
    {
        float length = 0f;
        Vector3 from = transform.position;

        for (int i = _points.Count - 1; i >= 0; i--)
        {
            length += Vector3.Distance(from, _points[i]);
            from = _points[i];

            if (length > _followDistance + 1f) // เผื่อไว้ 1 เมตร
            {
                _points.RemoveRange(0, i); // เก็บจุด i ไว้ ลบที่เก่ากว่านั้น
                return;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 1; i < _points.Count; i++)
            Gizmos.DrawLine(_points[i - 1], _points[i]);

        if (_trailPoint != null)
            Gizmos.DrawWireSphere(_trailPoint.position, 0.15f);
    }
}
