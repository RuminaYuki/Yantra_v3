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

        health.Kill();
        Debug.Log("[PlayerDebug] Kill Player", this);
    }

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
