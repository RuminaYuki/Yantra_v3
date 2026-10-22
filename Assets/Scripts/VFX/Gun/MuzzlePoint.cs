using System.Collections.Generic;
using UnityEngine;

// วางไว้ที่ปลายกระบอกปืน ลูกศรสีน้ำเงิน (แกน Z) ต้องชี้ออกไปทางที่ยิง
// ปืนที่ถูกซ่อน (สลับอาวุธ) จะไม่ถูกนับ ใช้ปืนที่เปิดอยู่ล่าสุด
public class MuzzlePoint : MonoBehaviour
{
    private static readonly List<MuzzlePoint> _active = new List<MuzzlePoint>();

    public static MuzzlePoint Active => _active.Count > 0 ? _active[_active.Count - 1] : null;

    private void OnEnable() => _active.Add(this);

    private void OnDisable() => _active.Remove(this);

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.02f);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 0.2f);
    }
}
