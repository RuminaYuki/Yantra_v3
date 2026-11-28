using UnityEngine;

/// <summary>
/// หลุมผีนาค (VFX_OccultHole) เปลี่ยนตามท่ามือ — ไม่ต้องแก้ table
///   มือโผล่สะบัดนิ้วรอ (Flick)      → Charging  หลุมเต้นตุบ ๆ ประกายค่อย ๆ สะสม
///   มือคว้าผู้เล่น (GrabQTE เริ่ม)  → Activated ควัน + ประกายพุ่งออกจากหลุม
///   มือมุดกลับ (GoBack) / ว่าง (Idle) → Idle      หลุมนิ่ง ๆ
/// วางบน EnemyController ของ PNak (ตัวเดียวกับ Animator) — เมนู Setup Nak Hole ใส่ + ลากช่องให้เอง
/// TypeB เป็น Variant ของ TypeA ได้ไปด้วยอัตโนมัติ
/// </summary>
public class NakHoleLink : MonoBehaviour
{
    [Tooltip("HoleStateController ของหลุม (VFX_OccultHole ลูกของตัวนี้)")]
    [SerializeField] private HoleStateController _hole;

    [Tooltip("Animator ของนาค")]
    [SerializeField] private Animator _animator;

    [Tooltip("GrabQTE ของนาคตัวนี้ — เริ่มจับเมื่อไหร่ หลุมระเบิดควัน")]
    [SerializeField] private GrabQTEInteractAddition _grab;

    [Tooltip("ชื่อ state ใน Animator ที่ให้หลุมเริ่มสะสมพลัง (Charging)")]
    [SerializeField] private string[] _chargingStates = { "Flick", "Showup" };

    [Tooltip("ชื่อ state ใน Animator ที่ให้หลุมกลับไปนิ่ง (Idle)")]
    [SerializeField] private string[] _idleStates = { "GoBack", "Idle" };

    private int[] _chargingHashes;
    private int[] _idleHashes;
    private int _lastHash;
    private bool _grabbing;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        _chargingHashes = ToHashes(_chargingStates);
        _idleHashes = ToHashes(_idleStates);
    }

    private void OnEnable()
    {
        GrabQTEInteractAddition.AnyStarted += HandleGrabStarted;
        GrabQTEInteractAddition.AnyEnded += HandleGrabEnded;
        _lastHash = 0;
        _grabbing = false;
        Set(HoleStateController.HoleState.Idle);   // หยิบจาก pool มาใหม่ เริ่มที่หลุมนิ่ง
    }

    private void OnDisable()
    {
        GrabQTEInteractAddition.AnyStarted -= HandleGrabStarted;
        GrabQTEInteractAddition.AnyEnded -= HandleGrabEnded;
    }

    private void HandleGrabStarted(GrabQTEInteractAddition grab)
    {
        if (grab != _grab) return;   // นาคตัวอื่นจับ ไม่เกี่ยว
        _grabbing = true;
        Set(HoleStateController.HoleState.Activated);
    }

    private void HandleGrabEnded(GrabQTEInteractAddition grab)
    {
        if (grab == _grab) _grabbing = false;   // ค้าง Activated ไว้จนมือเข้าท่า GoBack
    }

    private void Update()
    {
        if (_hole == null || _animator == null || !_animator.isActiveAndEnabled) return;

        AnimatorStateInfo info = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0)
            : _animator.GetCurrentAnimatorStateInfo(0);
        if (info.shortNameHash == _lastHash) return;
        _lastHash = info.shortNameHash;

        if (_grabbing) return;   // ระหว่างจับให้หลุมพ่นควันค้างไว้
        if (Contains(_chargingHashes, _lastHash)) Set(HoleStateController.HoleState.Charging);
        else if (Contains(_idleHashes, _lastHash)) Set(HoleStateController.HoleState.Idle);
    }

    private void Set(HoleStateController.HoleState state)
    {
        if (_hole != null && _hole.selectedState != state) _hole.SetState(state);
    }

    private static int[] ToHashes(string[] names)
    {
        if (names == null) return new int[0];
        var hashes = new int[names.Length];
        for (int i = 0; i < names.Length; i++) hashes[i] = Animator.StringToHash(names[i]);
        return hashes;
    }

    private static bool Contains(int[] hashes, int hash)
    {
        foreach (int h in hashes)
            if (h == hash) return true;
        return false;
    }
}
