using UnityEngine;

// หา component บนตัวผู้เล่นจาก tag
// ตัวผู้เล่นติด tag Player ทุกลูก — FindGameObjectWithTag คืนตัวไหนก็ได้ (แต่ละรอบ Play ไม่เหมือนกัน)
// ถ้าได้ตัวลูกที่ไม่มี component ที่ต้องการ ระบบจะหาไม่เจอแบบสุ่ม ๆ เลยต้องไล่ดูทุกตัว
public static class PlayerLookup
{
    public static T Find<T>(string tag = "Player") where T : Component
    {
        GameObject[] tagged = GameObject.FindGameObjectsWithTag(tag);

        foreach (GameObject obj in tagged)
        {
            if (obj.TryGetComponent(out T found))
                return found;
        }

        // ไม่มีตัวไหนถือไว้ตรง ๆ — ลองไล่ขึ้นไปทางตัวแม่
        foreach (GameObject obj in tagged)
        {
            T found = obj.GetComponentInParent<T>();
            if (found != null)
                return found;
        }

        return null;
    }
}