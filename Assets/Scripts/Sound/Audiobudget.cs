using UnityEngine;

/// <summary>
/// 
/// </summary>
public static class AudioListenerCache
{
    private static Transform cached;

    public static Transform Transform
    {
        get
        {
            if (cached == null)
            {
                AudioListener listener = Object.FindObjectOfType<AudioListener>();
                if (listener != null) cached = listener.transform;
            }
            return cached;
        }
    }

    /// <summary>ระยะห่างจากหูผู้ฟัง (ยกกำลังสอง - ไม่ต้องถอดรากให้เปลืองแรง)</summary>
    public static float SqrDistanceToListener(Vector3 position)
    {
        Transform listener = Transform;
        if (listener == null) return 0f;   // หาไม่เจอ = ให้ผ่านไปก่อน ดีกว่าเงียบทั้งเกม
        return (position - listener.position).sqrMagnitude;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        // static ไม่รีเซ็ตเองถ้าปิด Domain Reload ใน Editor
        // ไม่เคลียร์ตรงนี้ = กด Play รอบสองจะจำ listener ตัวเก่าที่ตายไปแล้ว
        cached = null;
    }
}

/// <summary>
/// </summary>
public static class CreatureFootstepBudget
{
    private const float WindowSeconds = 0.15f;
    private const int MaxStepsPerWindow = 3;

    private static float windowStartTime = -999f;
    private static int stepsInWindow = 0;

    /// <summary>
    /// ขอโควต้า 1 ช่อง คืน true = เล่นเสียงได้ / false = ช่องเต็ม ให้เงียบ
    /// </summary>
    public static bool TryConsume()
    {
        float now = Time.time;

        // หมดช่วงเวลาเดิมแล้ว เปิดช่องใหม่
        if (now - windowStartTime > WindowSeconds)
        {
            windowStartTime = now;
            stepsInWindow = 0;
        }

        if (stepsInWindow >= MaxStepsPerWindow) return false;

        stepsInWindow++;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        windowStartTime = -999f;
        stepsInWindow = 0;
    }
}