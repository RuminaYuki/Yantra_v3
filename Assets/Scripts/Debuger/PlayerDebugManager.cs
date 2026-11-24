using UnityEngine;

// รวมคำสั่ง debug ของผู้เล่น ไว้ผูกกับ Button (OnClick) ใน Debug Panel
// ไม่ต้องลาก ref ผู้เล่น — หาเองตอนกดปุ่ม (tag "Player") เลยใช้ได้ทุกฉาก
// และยังใช้ได้ถ้าผู้เล่น spawn ทีหลัง / เปลี่ยนฉากแล้วผู้เล่นตัวเก่าหายไป
public class PlayerDebugManager : MonoBehaviour
{
    [Tooltip("Tag ของตัวผู้เล่น")]
    [SerializeField] private string _playerTag = "Player";

    // จำไว้ไม่ต้องหาใหม่ทุกครั้ง — เปลี่ยนฉากแล้วตัวเก่าถูกทำลาย จะเป็น null แล้วหาใหม่เอง
    private Health _health;

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

    // ---------- หาผู้เล่น ----------

    private Health FindPlayerHealth()
    {
        if (_health == null)
            _health = PlayerLookup.Find<Health>(_playerTag);

        if (_health == null)
            Debug.LogWarning($"[PlayerDebug] หาผู้เล่น (tag \"{_playerTag}\") ที่มี Health ในฉากนี้ไม่เจอ", this);

        return _health;
    }
}
