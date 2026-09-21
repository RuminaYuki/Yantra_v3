using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ยิงแบบ hitscan รองรับการทะลุ "เฉพาะศัตรู (IDamageable)"
/// วัตถุอื่น เช่น กำแพง/พื้น จะหยุดกระสุนทันที
/// </summary>
public class HitscanShooter : MonoBehaviour
{
    [SerializeField] bool playOnAwake = false;
    [Header("Ray")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private float range = 100f;
    [Tooltip("ต้องรวมทั้ง layer ของศัตรูและ layer ของกำแพง/พื้น")]
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [Tooltip("Root ของผู้ยิง จะไม่ยิงโดนตัวเอง")]
    [SerializeField] private Transform ignoreRoot;

    [Header("Damage")]
    [SerializeField] private float damage = 25f;

    [Header("Penetration")]
    [Tooltip("จำนวนศัตรูที่ทะลุได้เพิ่มจากตัวแรก เช่น 1 = โดนได้ 2 ตัว, 2 = โดนได้ 3 ตัว, 0 = ตัวเดียว")]
    [Min(0)][SerializeField] private int maxPenetrations = 1;
    [Tooltip("ดาเมจที่เหลือหลังทะลุแต่ละตัว (0.7 = เหลือ 70%)")]
    [Range(0f, 1f)][SerializeField] private float damageRetainedPerPenetration = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = true;
    [SerializeField] private float debugDuration = 1f;

    /// <summary>เรียกทุกครั้งที่กระสุนโดนอะไรสักอย่าง (ศัตรูหรือกำแพง) ใช้ spawn VFX/เสียง</summary>
    public event Action<RaycastHit, int> HitRegistered;
    public event Action<Vector3, Vector3> ShotCompleted;

    private const int MaxHitsPerShot = 32;
    private readonly RaycastHit[] _hits = new RaycastHit[MaxHitsPerShot];
    private readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();
    private static readonly Comparer<RaycastHit> ByDistance =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    public int MaxPenetrations
    {
        get => maxPenetrations;
        set => maxPenetrations = Mathf.Max(0, value);
    }

    private void Awake()
    {
        if (playOnAwake) Fire();
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

        int count = Physics.RaycastNonAlloc(origin, direction, _hits, range, hitMask, triggerInteraction);
        Array.Sort(_hits, 0, count, ByDistance); // Unity ไม่รับประกันลำดับ ต้องเรียงเอง

        int penetrationsLeft = maxPenetrations;
        float currentDamage = damage;
        int hitIndex = 0;
        bool stopped = false;
        Vector3 endPoint = rangeEnd;
        _alreadyHit.Clear();

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];

            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
                continue;

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            // ไม่ใช่ศัตรู (กำแพง/พื้น/อื่นๆ) -> กระสุนหยุดที่นี่
            if (damageable == null)
            {
                HitRegistered?.Invoke(hit, hitIndex);
                endPoint = hit.point;
                stopped = true;
                break;
            }

            // ศัตรูตัวเดิมที่มีหลาย collider (ragdoll) นับครั้งเดียว
            if (!_alreadyHit.Add(damageable))
                continue;

            damageable.TakeDamage(currentDamage);
            HitRegistered?.Invoke(hit, hitIndex);
            hitIndex++;
            endPoint = hit.point;

            if (drawDebugRay)
                Debug.DrawRay(hit.point, hit.normal * 0.3f, Color.cyan, debugDuration);

            // ทะลุหมดแล้ว -> หยุดที่ศัตรูตัวนี้
            if (penetrationsLeft <= 0)
            {
                stopped = true;
                break;
            }

            penetrationsLeft--;
            currentDamage *= damageRetainedPerPenetration;

            ShotCompleted?.Invoke(origin, endPoint);
        }

        if (drawDebugRay)
        {
            // แดง = ส่วนที่กระสุนวิ่งจริง, เทา = ส่วนที่กระสุนไม่ไปถึง
            Debug.DrawLine(origin, endPoint, Color.red, debugDuration);
            if (stopped)
                Debug.DrawLine(endPoint, rangeEnd, Color.gray, debugDuration);
        }
    }
}