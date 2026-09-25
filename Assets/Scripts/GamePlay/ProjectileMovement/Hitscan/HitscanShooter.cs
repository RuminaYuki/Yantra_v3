using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ยิงแบบ hitscan (raycast หรือ spherecast ตาม radius) รองรับการทะลุ "เฉพาะศัตรู (IDamageable)"
/// วัตถุอื่น เช่น กำแพง/พื้น จะหยุดกระสุนทันที
/// </summary>
public class HitscanShooter : BaseProjectileMovement
{
    [Header("Ray")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private float range = 100f;
    [Tooltip("ความกว้าง (รัศมี) ของลำยิง ใส่ 0 = เท่ากับ raycast เส้นตรงปกติ")]
    [SerializeField] private float radius = 0f;
    [Tooltip("ต้องรวมทั้ง layer ของศัตรูและ layer ของกำแพง/พื้น")]
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("Layer ที่จะไม่ถูกตรวจชนและให้ลำยิงทะลุผ่าน")]
    [SerializeField] private LayerMask ignoredLayer;
    [Tooltip("Layer ที่ชนแล้วต้องหยุดทันที แม้จะมี IDamageable")]
    [SerializeField] private LayerMask nonPenetrableLayer;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [Tooltip("Root ของผู้ยิง จะไม่ยิงโดนตัวเอง")]
    [SerializeField] private Transform ignoreRoot;
    public void SetIgnoreRoot(Transform go) => ignoreRoot = go.transform;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = true;
    [SerializeField] private float debugDuration = 1f;

    /// <summary>เรียกทุกครั้งที่กระสุนโดนอะไรสักอย่าง (ศัตรูหรือกำแพง) ใช้ spawn VFX/เสียง</summary>
    public event Action<RaycastHit> raycastHit;
    public event Action<Vector3, Vector3> ShootCompleted;

    private const int MaxHitsPerShot = 32;
    private readonly RaycastHit[] _hits = new RaycastHit[MaxHitsPerShot];
    private readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();
    private static readonly Comparer<RaycastHit> ByDistance =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    protected override void Awake()
    {
        base.Awake();
    }

    public void Fire()
    {
        Transform origin = muzzle != null ? muzzle : transform;
        Fire(origin.position, origin.forward);
    }

    public void Fire(Vector3 origin, Vector3 direction)
    {
        direction.Normalize();
        Vector3 rangeEnd = origin + direction * range;
        int effectiveHitMask = hitMask.value & ~ignoredLayer.value;

        int count = radius > 0f
            ? Physics.SphereCastNonAlloc(origin, radius, direction, _hits, range, effectiveHitMask, triggerInteraction)
            : Physics.RaycastNonAlloc(origin, direction, _hits, range, effectiveHitMask, triggerInteraction);
        Array.Sort(_hits, 0, count, ByDistance); // Unity ไม่รับประกันลำดับ ต้องเรียงเอง

        int penetrationsLeft = MaxPenetrations;
        int hitIndex = 0;
        bool stopped = false;
        Vector3 endPoint = rangeEnd;
        _alreadyHit.Clear();

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];

            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
                continue;

            // Unity limitation: SphereCast ที่ทับ collider ตั้งแต่ origin (initial overlap)
            // จะคืน hit.point/normal เป็น (0,0,0) และ distance เป็น 0 เสมอ ไม่ว่าตำแหน่งจริงจะอยู่ที่ไหน
            // ต้องคำนวณจุดที่ใกล้ที่สุดเองแทน hit.point ที่เชื่อถือไม่ได้ในเคสนี้
            bool isInitialOverlap = radius > 0f && hit.distance <= 0f;
            Vector3 hitPoint = isInitialOverlap
                ? hit.collider.ClosestPoint(origin)
                : hit.point;

            bool isNonPenetrable = (nonPenetrableLayer.value & (1 << hit.collider.gameObject.layer)) != 0;
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            // Layer ที่กำหนดให้ทะลุไม่ได้ หรือไม่ใช่ศัตรู -> กระสุนหยุดที่นี่
            if (isNonPenetrable || damageable == null)
            {
                RaycastHit reportedHit = hit;
                if (isInitialOverlap)
                    reportedHit.point = hitPoint; // แก้ point ให้ถูกต้องก่อนส่งต่อ event

                raycastHit?.Invoke(reportedHit);
                HitRegisteredRaise(hit.collider);
                endPoint = hitPoint;
                stopped = true;
                break;
            }

            // ศัตรูตัวเดิมที่มีหลาย collider (ragdoll) นับครั้งเดียว
            if (!_alreadyHit.Add(damageable))
                continue;

            {
                RaycastHit reportedHit = hit;
                if (isInitialOverlap)
                    reportedHit.point = hitPoint;

                raycastHit?.Invoke(reportedHit);
            }
            HitRegisteredRaise(hit.collider);
            hitIndex++;

            if (drawDebugRay)
                Debug.DrawRay(hitPoint, (isInitialOverlap ? direction : hit.normal) * 0.3f, Color.cyan, debugDuration);

            // ทะลุหมดแล้ว -> หยุดที่ศัตรูตัวนี้
            if (penetrationsLeft <= 0)
            {
                endPoint = hitPoint;
                stopped = true;
                break;
            }

            penetrationsLeft--;
        }
        ShootCompleted?.Invoke(origin, endPoint);

        if (drawDebugRay)
        {
            // แดง = ส่วนที่กระสุนวิ่งจริง, เทา = ส่วนที่กระสุนไม่ไปถึง
            Debug.DrawLine(origin, endPoint, Color.red, debugDuration);
            if (stopped)
                Debug.DrawLine(endPoint, rangeEnd, Color.gray, debugDuration);
        }
    }
}