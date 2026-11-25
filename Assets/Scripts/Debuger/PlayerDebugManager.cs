using System.Collections.Generic;
using UnityEngine;

// รวมคำสั่ง debug ของผู้เล่น ไว้ผูกกับ Button (OnClick) ใน Debug Panel
// ไม่ต้องลาก ref ผู้เล่น — หาเองตอนกดปุ่ม (tag "Player") เลยใช้ได้ทุกฉาก
// และยังใช้ได้ถ้าผู้เล่น spawn ทีหลัง / เปลี่ยนฉากแล้วผู้เล่นตัวเก่าหายไป
public class PlayerDebugManager : MonoBehaviour
{
    [Tooltip("Tag ของตัวผู้เล่น")]
    [SerializeField] private string _playerTag = "Player";

    // กลุ่มจุดวาร์ป — พับเก็บใน Inspector ได้ ชื่อกลุ่มโชว์เป็นหัวข้อ
    [System.Serializable]
    private class TeleportGroup
    {
        public string name;
        public List<Transform> points = new List<Transform>();
    }

    [Tooltip("จุดวาร์ปแบ่งกลุ่ม — ปุ่มใช้ DebugTeleportButton ใส่ Group (กลุ่มที่เท่าไหร่) + Point (จุดในกลุ่ม เริ่ม 0 ทุกกลุ่ม)\n" +
             "เพิ่ม/ลบจุดในกลุ่มไหนก็ได้ ปุ่มของกลุ่มอื่นไม่เลื่อน — แต่อย่าสลับลำดับกลุ่ม (เลข Group จะเปลี่ยน)\n" +
             "ผู้เล่นจะยืนตำแหน่งนี้และหันตามทิศ (ลูกศรสีน้ำเงิน) ของจุด")]
    [SerializeField] private List<TeleportGroup> _teleportGroups = new List<TeleportGroup>();

    [Tooltip("วาร์ปเสร็จแล้วปิด Debug panel ให้ (กล้องจะได้ตามผู้เล่นไปทันที)\nเว้นว่าง = หาจากตัวเอง / parent เอง")]
    [SerializeField] private DebugPanel _debugPanel;
    [SerializeField] private bool _closePanelAfterTeleport = true;

    // ติ๊กก่อน Play = เปิดตั้งแต่เริ่ม / ติ๊กระหว่างเล่น = เปิดปิดทันที / กดปุ่มใน panel ช่องก็ขยับตาม
    [Header("สถานะ (ติ๊กระหว่างเล่นได้)")]
    [Tooltip("เลือดไม่ลด (ยังโดนตี / มีท่าเจ็บ / knockback ปกติ)")]
    [SerializeField] private bool _godMode;
    [Tooltip("หมัดแรงขึ้นตาม Strong Attack Multiplier")]
    [SerializeField] private bool _strongAttack;
    [Tooltip("เดิน / วิ่ง / dash เร็วขึ้นตาม Fast Run Multiplier")]
    [SerializeField] private bool _fastRun;
    [Tooltip("ปืนยิงได้ไม่ต้องมีกระสุน และไม่หักของ")]
    [SerializeField] private bool _infiniteAmmo;
    [Tooltip("เวทใช้ได้ไม่ต้องมีวัตถุดิบ และไม่หักของ")]
    [SerializeField] private bool _infiniteSpellMaterial;
    [Tooltip("Guard point เต็มตลอด")]
    [SerializeField] private bool _infiniteGuard;

    [Header("ค่าที่ใช้ตอนเปิด")]
    [Tooltip("ตัวคูณดาเมจหมัดตอนเปิด Strong Attack")]
    [SerializeField, Min(0f)] private float _strongAttackMultiplier = 1000f;
    [Tooltip("ตัวคูณความเร็วเดิน / วิ่ง / dash ตอนเปิด Fast Run")]
    [SerializeField, Min(0f)] private float _fastRunMultiplier = 3f;

    private Health _godHealth;   // ตัวที่ผูก event ไว้อยู่
    private bool _killing;       // KillPlayer กำลังทำงาน — god mode ห้ามเติมเลือดกลับ
    private float _nextPlayerCheckTime;

    // สิ่งที่ตั้งไว้บนตัวผู้เล่นจริงแล้ว — ผู้เล่นตัวนี้ถูกทำลาย (เกิดใหม่ / เปลี่ยนฉาก) ค่อยตั้งให้ตัวใหม่
    private bool _strongApplied, _fastApplied, _ammoApplied;
    private GameObject _appliedPlayer;

    public bool GodMode => _godMode;

    // ใช้กับ DebugCheatToggle — ปุ่มเดียวเลือกได้ว่าคุมโหมดไหน
    public enum Cheat
    {
        GodMode,
        StrongAttack,
        FastRun,
        InfiniteAmmo,
        InfiniteSpellMaterial,
        InfiniteGuard,
    }

    public bool IsOn(Cheat cheat) => cheat switch
    {
        Cheat.GodMode => _godMode,
        Cheat.StrongAttack => _strongAttack,
        Cheat.FastRun => _fastRun,
        Cheat.InfiniteAmmo => _infiniteAmmo,
        Cheat.InfiniteSpellMaterial => RadialMenuButtonData.IgnoreItemCost,
        Cheat.InfiniteGuard => BlockSystem.InfiniteGuard,
        _ => false,
    };

    public void Set(Cheat cheat, bool on)
    {
        switch (cheat)
        {
            case Cheat.GodMode: SetGodMode(on); break;
            case Cheat.StrongAttack: SetStrongAttack(on); break;
            case Cheat.FastRun: SetFastRun(on); break;
            case Cheat.InfiniteAmmo: SetInfiniteAmmo(on); break;
            case Cheat.InfiniteSpellMaterial: SetInfiniteSpellMaterial(on); break;
            case Cheat.InfiniteGuard: SetInfiniteGuard(on); break;
        }
    }

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

    // DebugTeleportButton เรียก — group = กลุ่มที่เท่าไหร่ใน _teleportGroups, point = จุดในกลุ่ม (เริ่ม 0)
    public void TeleportPlayer(int group, int point)
    {
        if (group < 0 || group >= _teleportGroups.Count || _teleportGroups[group] == null)
        {
            Debug.LogWarning($"[PlayerDebug] Teleport: ไม่มีกลุ่มที่ {group} (มี {_teleportGroups.Count} กลุ่ม: 0 - {_teleportGroups.Count - 1})", this);
            return;
        }

        TeleportGroup teleportGroup = _teleportGroups[group];
        List<Transform> points = teleportGroup.points;
        if (points == null || point < 0 || point >= points.Count)
        {
            int count = points != null ? points.Count : 0;
            Debug.LogWarning($"[PlayerDebug] Teleport: กลุ่ม {teleportGroup.name} ไม่มีจุดที่ {point} (มี {count} จุด: 0 - {count - 1})", this);
            return;
        }

        Transform target = points[point];
        string groupName = teleportGroup.name;
        if (target == null)
        {
            Debug.LogWarning($"[PlayerDebug] Teleport: {groupName} จุดที่ {point} ว่างอยู่ (ยังไม่ได้ลากใส่ / ถูกลบไป)", this);
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

        Debug.Log($"[PlayerDebug] Teleport → {groupName} [{point}]: {target.name}", this);

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
            _strongAttack = _strongApplied;   // ตั้งไม่สำเร็จ — ช่องสถานะกลับเป็นค่าจริง
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

        _strongAttack = _strongApplied = on;
        _appliedPlayer = player.transform.root.gameObject;
    }

    // ---------- Fast Run ----------

    // Toggle (UI) → On Value Changed → SetFastRun (Dynamic bool)
    public void SetFastRun(bool on)
    {
        BaseLocomotion locomotion = PlayerLookup.Find<PlayerLocomotion>(_playerTag);
        if (locomotion == null)
        {
            Debug.LogWarning($"[PlayerDebug] Fast Run: หาผู้เล่น (tag \"{_playerTag}\") ที่มี PlayerLocomotion ไม่เจอ", this);
            _fastRun = _fastApplied;
            return;
        }

        locomotion.SpeedMultiplier = on ? _fastRunMultiplier : 1f;
        Debug.Log($"[PlayerDebug] Fast Run: {(on ? $"x{_fastRunMultiplier}" : "OFF")}", this);

        _fastRun = _fastApplied = on;
        _appliedPlayer = locomotion.transform.root.gameObject;
    }

    // ---------- Infinite Ammo ----------

    // GunController.useItemBullet เป็น private ไม่มี setter — ใช้ reflection จะได้ไม่ต้องแก้ไฟล์ปืน
    // false = ยิงได้โดยไม่ต้องมีกระสุน และไม่หักของในกระเป๋า (ระบบมีอยู่แล้วใน GunController)
    private static readonly System.Reflection.FieldInfo UseItemBulletField =
        typeof(GunController).GetField("useItemBullet",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    // Toggle (UI) → On Value Changed → SetInfiniteAmmo (Dynamic bool)
    public void SetInfiniteAmmo(bool on)
    {
        if (UseItemBulletField == null)
        {
            Debug.LogWarning("[PlayerDebug] Infinite Ammo: GunController ไม่มี field \"useItemBullet\" แล้ว (ถูกเปลี่ยนชื่อ?)", this);
            _infiniteAmmo = _ammoApplied;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
        if (player == null)
        {
            Debug.LogWarning($"[PlayerDebug] Infinite Ammo: หาผู้เล่น (tag \"{_playerTag}\") ไม่เจอ", this);
            _infiniteAmmo = _ammoApplied;
            return;
        }

        GunController[] guns = player.transform.root.GetComponentsInChildren<GunController>(true);
        foreach (GunController gun in guns)
            UseItemBulletField.SetValue(gun, !on);

        if (guns.Length == 0)
            Debug.LogWarning("[PlayerDebug] Infinite Ammo: ผู้เล่นไม่มี GunController", this);
        else
            Debug.Log($"[PlayerDebug] Infinite Ammo: {(on ? "ON" : "OFF")} ({guns.Length} กระบอก)", this);

        _infiniteAmmo = _ammoApplied = on;
        _appliedPlayer = player.transform.root.gameObject;
    }

    // ---------- Infinite Spell Material ----------

    // Toggle (UI) → On Value Changed → SetInfiniteSpellMaterial (Dynamic bool)
    // ทุกเวทในวงล้อใช้ได้โดยไม่ต้องมีวัตถุดิบ และไม่หักของ — ค่าเป็น static ใช้ได้ทุกฉาก
    public void SetInfiniteSpellMaterial(bool on)
    {
        RadialMenuButtonData.IgnoreItemCost = _infiniteSpellMaterial = on;
        Debug.Log($"[PlayerDebug] Infinite Spell Material: {(on ? "ON" : "OFF")}", this);
    }

    // ---------- Infinite Guard ----------

    // Toggle (UI) → On Value Changed → SetInfiniteGuard (Dynamic bool)
    // guard เต็มตลอด — ค่าเป็น static ใช้ได้ทุกฉาก ผู้เล่นเกิดใหม่ก็ยังเปิดอยู่
    public void SetInfiniteGuard(bool on)
    {
        BlockSystem.InfiniteGuard = _infiniteGuard = on;
        Debug.Log($"[PlayerDebug] Infinite Guard: {(on ? "ON" : "OFF")}", this);
    }

    // ---------- Spell Unlock ----------

    private SpellUnlocks _spellUnlocks;
    private float _nextSpellLookupTime;

    // spellId ต้องตรงกับใน Yant.asset เช่น "SelfHeal", "Homing Missile", "Push"
    // ใช้ Unlock / Lock ของจริง — ถ้าเซฟเกมตอนปลดล็อคอยู่ จะติดไปในไฟล์เซฟด้วย
    public bool IsSpellUnlocked(string spellId)
    {
        SpellUnlocks unlocks = FindSpellUnlocks(warn: false);
        return unlocks != null && unlocks.IsUnlocked(spellId);
    }

    public void SetSpellUnlocked(string spellId, bool unlocked)
    {
        SpellUnlocks unlocks = FindSpellUnlocks(warn: true);
        if (unlocks == null)
            return;

        if (unlocked)
            unlocks.Unlock(spellId, this);
        else
            unlocks.Lock(spellId, this);
        Debug.Log($"[PlayerDebug] Spell \"{spellId}\": {(unlocked ? "Unlock" : "Lock")}", this);
    }

    // Button → OnClick → ToggleSpell แล้วพิมพ์ spellId
    public void ToggleSpell(string spellId) => SetSpellUnlocked(spellId, !IsSpellUnlocked(spellId));

    private SpellUnlocks FindSpellUnlocks(bool warn)
    {
        // หาไม่เจอ → รอ 1 วิค่อยหาใหม่ (DebugSpellToggle เรียกทุกเฟรม)
        if (_spellUnlocks == null && (warn || Time.unscaledTime >= _nextSpellLookupTime))
        {
            _nextSpellLookupTime = Time.unscaledTime + 1f;
            GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
            if (player != null)
                _spellUnlocks = player.transform.root.GetComponentInChildren<SpellUnlocks>(true);
        }

        if (_spellUnlocks == null && warn)
            Debug.LogWarning($"[PlayerDebug] หา SpellUnlocks บนผู้เล่น (tag \"{_playerTag}\") ไม่เจอ", this);

        return _spellUnlocks;
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

    // ---------- สถานะ ----------

    // ติ๊กไว้ก่อน Play = เปิดตั้งแต่เริ่ม (God Mode ผูกเองใน Update)
    private void Start()
    {
        if (_strongAttack) SetStrongAttack(true);
        if (_fastRun) SetFastRun(true);
        if (_infiniteAmmo) SetInfiniteAmmo(true);
        if (_infiniteSpellMaterial) SetInfiniteSpellMaterial(true);
        if (_infiniteGuard) SetInfiniteGuard(true);
    }

#if UNITY_EDITOR
    // ติ๊กช่องสถานะใน Inspector ระหว่างเล่น → เปิด/ปิดจริง
    private void OnValidate()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        if (_godMode != (_godHealth != null)) SetGodMode(_godMode);
        if (_strongAttack != _strongApplied) SetStrongAttack(_strongAttack);
        if (_fastRun != _fastApplied) SetFastRun(_fastRun);
        if (_infiniteAmmo != _ammoApplied) SetInfiniteAmmo(_infiniteAmmo);
        if (_infiniteSpellMaterial != RadialMenuButtonData.IgnoreItemCost) SetInfiniteSpellMaterial(_infiniteSpellMaterial);
        if (_infiniteGuard != BlockSystem.InfiniteGuard) SetInfiniteGuard(_infiniteGuard);
    }
#endif

    private void Update()
    {
        // ค่า static อาจถูกเปลี่ยนจากที่อื่น — ช่องสถานะตามค่าจริง
        _infiniteSpellMaterial = RadialMenuButtonData.IgnoreItemCost;
        _infiniteGuard = BlockSystem.InfiniteGuard;

        // ลองหาทุก 1 วิ ไม่ใช่ทุกเฟรม — ฉากที่ไม่มีผู้เล่น (เช่น Main Menu) จะได้ไม่หาวนทั้งเกม
        if (Time.unscaledTime < _nextPlayerCheckTime)
            return;
        _nextPlayerCheckTime = Time.unscaledTime + 1f;

        // God Mode: ผู้เล่น spawn ใหม่ / เปลี่ยนฉาก (ตัวเก่าถูกทำลาย) — ผูกกับตัวใหม่
        if (_godMode && _godHealth == null)
        {
            _health = PlayerLookup.Find<Health>(_playerTag);   // หาเงียบๆ ไม่เตือน
            if (_health != null)
                BindGodMode();
        }

        // ผู้เล่นที่เคยตั้งค่าไว้หายไป — ตั้งให้ตัวใหม่ (ช่องสถานะจะได้ตรงกับของจริง)
        bool anyPlayerCheat = _strongApplied || _fastApplied || _ammoApplied;
        if (anyPlayerCheat && _appliedPlayer == null
            && GameObject.FindGameObjectWithTag(_playerTag) != null)
        {
            if (_strongApplied) SetStrongAttack(true);
            if (_fastApplied) SetFastRun(true);
            if (_ammoApplied) SetInfiniteAmmo(true);
        }
    }

    // ---------- God Mode ----------

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
