using UnityEngine;

// ปุ่ม Debug: ฆ่าผีทุกตัวในห้องที่ผู้เล่นยืนอยู่
// "ห้อง" = กล่อง BoxCollider ของ EnemyGroupTracker ที่มีตัวผู้เล่นอยู่ข้างใน
// ฆ่าผ่าน Health.Kill() = ทางเดียวกับตายจริง (tracker นับ / barrier ปลดล็อค / ดรอปของ / dissolve / save)
// ผูก OnClick ของปุ่มใน Debug panel → DebugKillRoomEnemies.KillAll
public class DebugKillRoomEnemies : MonoBehaviour
{
    public void KillAll()
    {
        PlayerLocomotion player = PlayerLookup.Find<PlayerLocomotion>();
        if (player == null)
        {
            Debug.LogWarning("[DebugKillRoomEnemies] หาผู้เล่นไม่เจอ", this);
            return;
        }

        Vector3 playerPos = player.transform.position;
        int killed = 0;
        bool inAnyRoom = false;

        foreach (EnemyGroupTracker room in FindObjectsByType<EnemyGroupTracker>(FindObjectsSortMode.None))
        {
            if (!room.RoomBounds.Contains(playerPos))
                continue;

            inAnyRoom = true;
            foreach (Health enemy in room.Enemies)
            {
                // ข้ามตัวที่ไม่มี / ยังไม่ถูกเปิด / ตายแล้ว
                if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.IsDead)
                    continue;

                enemy.Kill();
                killed++;
            }
        }

        if (!inAnyRoom)
            Debug.LogWarning("[DebugKillRoomEnemies] ผู้เล่นไม่ได้อยู่ในห้องไหน (กล่อง EnemyGroupTracker)", this);
        else
            Debug.Log($"[DebugKillRoomEnemies] ฆ่าไป {killed} ตัว", this);
    }
}
