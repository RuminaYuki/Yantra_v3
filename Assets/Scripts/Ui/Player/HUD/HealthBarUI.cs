using UnityEngine;

/// <summary>
/// แถบเลือด — อ่านจาก Health ของผู้เล่น
/// แปะบน GameObject ของแถบใน HUD
/// </summary>
public class HealthBarUI : StatBarUI
{
    [Header("Source")]
    [Tooltip("เว้นว่างได้ จะหาจาก GameObject ที่มี tag Player ให้เอง")]
    [SerializeField] private Health _health;

    [Tooltip("tag ที่ใช้ค้นหาผู้เล่น")]
    [SerializeField] private string _playerTag = "Player";

    private float _retryTimer;

    protected override bool TryReadValue(out float current, out float max)
    {
        current = 0f;
        max = 0f;

        if (_health == null)
        {
            // ลองหาใหม่ทุกครึ่งวินาที เผื่อผู้เล่นถูก spawn ทีหลัง
            _retryTimer += Time.deltaTime;
            if (_retryTimer < 0.5f) return false;
            _retryTimer = 0f;

            var playerObj = GameObject.FindGameObjectWithTag(_playerTag);
            if (playerObj != null) _health = playerObj.GetComponent<Health>();

            if (_health == null) return false;
        }

        current = _health.CurrentHP;
        max = _health.MaxHealth;
        return true;
    }
}
