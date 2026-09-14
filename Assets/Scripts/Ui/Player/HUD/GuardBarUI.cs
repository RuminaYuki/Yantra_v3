using UnityEngine;

/// <summary>
/// แถบ Guard Point — อ่านจาก BlockSystem ของผู้เล่น
/// แปะบน GameObject ของแถบใน HUD
///
/// ควรตั้ง Hide When Full = true เพราะ Guard ไม่ต้องโชว์ตลอดเวลา
/// จอที่สะอาดเหมาะกับเกมหลอนมากกว่า และทำให้ตอนมันโผล่มามีความหมายทันที
/// </summary>
public class GuardBarUI : StatBarUI
{
    [Header("Source")]
    [Tooltip("เว้นว่างได้ จะหาจาก GameObject ที่มี tag Player ให้เอง")]
    [SerializeField] private BlockSystem _blockSystem;

    [Tooltip("tag ที่ใช้ค้นหาผู้เล่น")]
    [SerializeField] private string _playerTag = "Player";

    private float _retryTimer;

    protected override bool TryReadValue(out float current, out float max)
    {
        current = 0f;
        max = 0f;

        if (_blockSystem == null)
        {
            _retryTimer += Time.deltaTime;
            if (_retryTimer < 0.5f) return false;
            _retryTimer = 0f;

            var playerObj = GameObject.FindGameObjectWithTag(_playerTag);
            if (playerObj != null) _blockSystem = playerObj.GetComponent<BlockSystem>();

            if (_blockSystem == null) return false;
        }

        current = _blockSystem.CurrentGuardPoints;
        max = _blockSystem.MaxGuardPoints;
        return true;
    }
}
