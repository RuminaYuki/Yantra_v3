using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// จุดยึดปลายเส้นเนื้อของ FleshStrands — เช่นตอคอที่ขาดของกระสือ
///
/// วางบน Empty ใต้กระดูกคอ (neck.x) ตรงรอยขาด
/// ก้อนเนื้อที่ตั้ง Tether = Nearest Anchor จะหาจุดนี้เองตอนเกิด (ตัวที่ใกล้ที่สุด)
/// เลยไม่ต้องแก้สคริปต์ยิง และกระสือหลายตัวในฉากก็ไม่ปนกัน
/// </summary>
public class FleshStrandAnchor : MonoBehaviour
{
    private static readonly List<FleshStrandAnchor> _all = new();

    /// <summary>มีก้อนเนื้อมายึดจุดนี้ — HeldFleshChunk ใช้รู้ว่าก้อนจริงถูกขว้างออกไปแล้ว</summary>
    public event Action<FleshStrands> Claimed;

    private void OnEnable() => _all.Add(this);
    private void OnDisable() => _all.Remove(this);

    // ปิด Domain Reload อยู่ — ล้างรายชื่อเก่าทุกครั้งที่กด Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _all.Clear();

    /// <summary>จุดยึดที่ใกล้ point ที่สุดภายในระยะ — ไม่เจอคืน null</summary>
    public static FleshStrandAnchor FindNearest(Vector3 point, float maxDistance)
    {
        FleshStrandAnchor best = null;
        float bestSqr = maxDistance * maxDistance;

        foreach (var anchor in _all)
        {
            if (anchor == null) continue;
            float sqr = (anchor.transform.position - point).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = anchor;
        }

        return best;
    }

    internal void NotifyClaimed(FleshStrands by) => Claimed?.Invoke(by);

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.04f);
    }
}
