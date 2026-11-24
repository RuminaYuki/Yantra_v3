using System.Collections.Generic;
using UnityEngine;

// รวมคำสั่ง debug ของผู้เล่น ไว้ผูกกับ Button (OnClick) ใน Debug Panel
// ไม่ต้องลาก ref ผู้เล่น — หาเองตอนกดปุ่ม (tag "Player") เลยใช้ได้ทุกฉาก
// และยังใช้ได้ถ้าผู้เล่น spawn ทีหลัง / เปลี่ยนฉากแล้วผู้เล่นตัวเก่าหายไป
public class PlayerDebugManager : MonoBehaviour
{
    [Tooltip("Tag ของตัวผู้เล่น")]
    [SerializeField] private string _playerTag = "Player";

    [Tooltip("จุดวาร์ป — ปุ่มเรียก TeleportPlayer(เลข index) เช่น 0 = ตัวแรกในรายการ\n" +
             "ผู้เล่นจะยืนตำแหน่งนี้และหันตามทิศ (ลูกศรสีน้ำเงิน) ของจุด")]
    [SerializeField] private List<Transform> _teleportPoints = new List<Transform>();

    [Tooltip("วาร์ปเสร็จแล้วปิด Debug panel ให้ (กล้องจะได้ตามผู้เล่นไปทันที)\nเว้นว่าง = หาจากตัวเอง / parent เอง")]
    [SerializeField] private DebugPanel _debugPanel;
    [SerializeField] private bool _closePanelAfterTeleport = true;

    [Header("God Mode")]
    [Tooltip("เลือดไม่ลด (ยังโดนตี / มีท่าเจ็บ / knockback ปกติ) — ติ๊กไว้ก่อน Play = เปิดตั้งแต่เริ่ม")]
    [SerializeField] private bool _godMode;

    private Health _godHealth;   // ตัวที่ผูก event ไว้อยู่
    private bool _killing;       // KillPlayer กำลังทำงาน — god mode ห้ามเติมเลือดกลับ
    private float _nextGodBindTime;

    public bool GodMode => _godMode;

    [Header("Attack")]
    [Tooltip("ตัวคูณดาเมจหมัดตอนเปิด Strong Attack")]
    [SerializeField, Min(0f)] private float _strongAttackMultiplier = 1000f;

    // จำไว้ไม่ต้องหาใหม่ทุกครั้ง — เปลี่ยนฉากแล้วตัวเก่าถูกทำลาย จะเป็น null แล้วหาใหม่เอง
    private Health _health;
    private CharacterController _controller;

    // ---------- ปุ่ม ----------

    public void KillPlayer()
    {
        Health health = FindPlayerHealth();
        if (health == null)
            return;

        if (health.IsDead)
        {
            Debug.Log("[PlayerDebug] ผู้เล่นตายอยู่แล้ว", this);
            return;
        }

        // ไม่งั้น god mode เติมเลือดกลับ → ตายทั้งที่เลือดเต็ม
        _killing = true;
        health.Kill();
        _killing = false;
        Debug.Log("[PlayerDebug] Kill Player", this);
    }

    // Toggle (UI) → On Value Changed → SetGodMode (Dynamic bool)
    public void SetGodMode(bool on)
    {
        _godMode = on;
        if (on)
            BindGodMode();
        else
            UnbindGodMode();
        Debug.Log($"[PlayerDebug] God Mode: {(on ? "ON" : "OFF")}", this);
    }

    // Button → ToggleGodMode
    public void ToggleGodMode() => SetGodMode(!_godMode);

    // OnClick ใส่เลข index ของจุดใน _teleportPoints
    public void TeleportPlayer(int index)
    {
        if (index < 0 || index >= _teleportPoints.Count)
        {
            Debug.LogWarning($"[PlayerDebug] Teleport: ไม่มีจุดที่ {index} (มี {_teleportPoints.Count} จุด: 0 - {_teleportPoints.Count - 1})", this);
            return;
        }

        Transform target = _teleportPoints[index];
        if (target == null)
        {
            Debug.LogWarning($"[PlayerDebug] Teleport: จุดที่ {index} ว่างอยู่ (ยังไม่ได้ลากใส่ / ถูกลบไป)", this);
            return;
        }

        CharacterController controller = FindPlayerController();
        if (controller == null)
            return;

        // CharacterController ดึงตำแหน่งกลับถ้ายังเปิดอยู่ — ปิดก่อนย้าย แบบเดียวกับ PlayerSaveable
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        controller.transform.SetPositionAndRotation(target.position, target.rotation);
        controller.enabled = wasEnabled;

        Debug.Log($"[PlayerDebug] Teleport → {index}: {target.name}", this);

        // panel เปิดอยู่ = กล้อง pause ไม่ตามผู้เล่น — ปิด panel ให้กล้องตามไปจุดใหม่ทันที
        if (_closePanelAfterTeleport)
        {
            if (_debugPanel == null)
                _debugPanel = GetComponentInParent<DebugPanel>();
            if (_debugPanel != null)
                _debugPanel.Close();
        }
    }

    // ---------- Attack ----------

    // Toggle (UI) → On Value Changed → SetStrongAttack (Dynamic bool)
    public void SetStrongAttack(bool on)
    {
        GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
        if (player == null)
        {
            Debug.LogWarning($"[PlayerDebug] Strong Attack: หาผู้เล่น (tag \"{_playerTag}\") ไม่เจอ", this);
            return;
        }

        // ไล่จาก root ของผู้เล่น — AttackSphereCast อาจอยู่ที่ลูก (มือ / อาวุธ)
        AttackSphereCast[] attacks = player.transform.root.GetComponentsInChildren<AttackSphereCast>(true);
        foreach (AttackSphereCast attack in attacks)
            attack.DamageMultiplier = on ? _strongAttackMultiplier : 1f;

        if (attacks.Length == 0)
            Debug.LogWarning("[PlayerDebug] Strong Attack: ผู้เล่นไม่มี AttackSphereCast", this);
        else
            Debug.Log($"[PlayerDebug] Strong Attack: {(on ? $"x{_strongAttackMultiplier}" : "OFF")} ({attacks.Length} ตัว)", this);
    }

    // ---------- State Flags ----------

    // Button → OnClick → ToggleFlag แล้วลาก FlagSO (เช่น PlayerEnableGun_Flag) ใส่ช่องข้างล่าง
    public void ToggleFlag(FlagSO flag) => SetFlag(flag, null);
    public void EnableFlag(FlagSO flag) => SetFlag(flag, true);
    public void DisableFlag(FlagSO flag) => SetFlag(flag, false);

    // value = null → สลับค่า
    private void SetFlag(FlagSO flag, bool? value)
    {
        if (flag == null)
        {
            Debug.LogWarning("[PlayerDebug] Flag: ยังไม่ได้ลาก FlagSO ใส่ปุ่ม", this);
            return;
        }

        // ตั้งทุก StateFlags ที่มี flag นี้ — ไม่ต้องรู้ว่าอยู่บนผู้เล่นหรือ object อื่น (เช่น ประตู)
        int count = 0;
        foreach (StateFlags owner in FindObjectsByType<StateFlags>())
        {
            if (!owner.Contains(flag))
                continue;

            bool newValue = value ?? !owner.Get(flag);
            owner.Set(flag, newValue);   // ผ่าน Set → ยิง FlagChanged ให้ระบบอื่นรู้
            count++;
            Debug.Log($"[PlayerDebug] {flag.name} = {newValue} ({owner.name})", owner);
        }

        if (count == 0)
            Debug.LogWarning($"[PlayerDebug] ไม่มี StateFlags ไหนในฉากที่มี {flag.name}", this);
    }

    // ---------- God Mode ----------

    private void Update()
    {
        // ผู้เล่น spawn ใหม่ / เปลี่ยนฉาก (ตัวเก่าถูกทำลาย) — ผูกกับตัวใหม่
        // ลองหาทุก 1 วิ ไม่ใช่ทุกเฟรม — ฉากที่ไม่มีผู้เล่น (เช่น Main Menu) จะได้ไม่หาวนทั้งเกม
        if (_godMode && _godHealth == null && Time.unscaledTime >= _nextGodBindTime)
        {
            _nextGodBindTime = Time.unscaledTime + 1f;
            _health = PlayerLookup.Find<Health>(_playerTag);   // หาเงียบๆ ไม่เตือน
            if (_health != null)
                BindGodMode();
        }
    }

    private void OnDestroy() => UnbindGodMode();

    private void BindGodMode()
    {
        Health health = FindPlayerHealth();
        if (health == null || health == _godHealth)
            return;

        UnbindGodMode();
        _godHealth = health;
        _godHealth.OnHealthChanged += OnGodHealthChanged;
        if (!_godHealth.IsDead)
            _godHealth.RestoreFullHealth();
    }

    private void UnbindGodMode()
    {
        if (_godHealth != null)
            _godHealth.OnHealthChanged -= OnGodHealthChanged;
        _godHealth = null;
    }

    // Health.TakeDamage ยิง OnHealthChanged ก่อนเช็คตาย → เติมเต็มตรงนี้ = ไม่ตาย
    // ไม่ใช้ SetEnableIgnoreDamage เพราะ BlockSystem / state machine เปิดปิดค่านั้นทับตลอด
    private void OnGodHealthChanged(float hp)
    {
        // เช็ค Max กันวนซ้ำ (RestoreFullHealth ยิง event อีกรอบ)
        if (_killing || hp >= _godHealth.MaxHealth)
            return;
        _godHealth.RestoreFullHealth();
    }

    // ---------- หาผู้เล่น ----------

    private CharacterController FindPlayerController()
    {
        if (_controller == null)
            _controller = PlayerLookup.Find<CharacterController>(_playerTag);

        if (_controller == null)
            Debug.LogWarning($"[PlayerDebug] หาผู้เล่น (tag \"{_playerTag}\") ที่มี CharacterController ในฉากนี้ไม่เจอ", this);

        return _controller;
    }

    private Health FindPlayerHealth()
    {
        if (_health == null)
            _health = PlayerLookup.Find<Health>(_playerTag);

        if (_health == null)
            Debug.LogWarning($"[PlayerDebug] หาผู้เล่น (tag \"{_playerTag}\") ที่มี Health ในฉากนี้ไม่เจอ", this);

        return _health;
    }
}
