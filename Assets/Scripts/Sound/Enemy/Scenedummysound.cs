using UnityEngine;

/// <summary>
/// เสียงลูปสำหรับ "ตัวประกอบฉาก" (dummy) ที่อยู่ท่าเดียวตลอดกาล
///
/// เช่น ผีที่ยืนกินซาก คนที่นั่งร้องไห้ ตัวที่ขูดผนัง
/// พวกนี้ไม่ต้องการระบบเสียงพูด/gacha/ฝีเท้าแบบผีจริง ต้องการแค่เสียงลูปเสียงเดียว
///
/// ไม่ต้องใส่ Animation Event ใดๆ ทั้งสิ้น — เสียงเริ่มเองตอน object ตื่น
/// จึงไม่ต้องไปแตะคลิปอนิเมชันซึ่งมักเป็นของคนอื่น
/// </summary>
public class SceneDummySound : MonoBehaviour
{
    [Header("Sound")]
    [Tooltip("เสียงลูปประจำตัว เช่นเสียงเคี้ยว เสียงร้องไห้")]
    [SerializeField] private SoundID loopSound;

    [Header("Transition")]
    [SerializeField] private float fadeInTime = 1.5f;
    [SerializeField] private float fadeOutTime = 0.8f;

    [Header("Performance (Optional)")]
    [Tooltip("เปิดเพื่อให้เล่นเฉพาะตอนผู้เล่นอยู่ในระยะ" +
        "\n\nทำไมควรเปิด: Unity เล่นเสียงพร้อมกันได้จริงแค่ 32 เสียง" +
        "\nถ้าวาง dummy 10 ตัวทั่วแมพแล้วเปิดเสียงค้างหมด = กิน 10 ช่องตลอดเวลา" +
        "\nทั้งที่ผู้เล่นได้ยินทีละตัว แล้วเสียงสำคัญอย่างเสียงกรี๊ดผีจะโดนตัดแทน" +
        "\n\nปิดได้ถ้ามีตัวเดียวและอยากให้เรียบง่ายที่สุด")]
    [SerializeField] private bool useDistanceCulling = true;

    [Tooltip("ผู้เล่นเข้าใกล้กว่านี้ (เมตร) ถึงจะเริ่มเล่น" +
        "\nควรมากกว่า Max Distance ใน SoundData สัก 3-5 เมตร" +
        "\nเพื่อให้เสียงเฟดเข้ามาก่อนที่จะเริ่มได้ยินจริง ไม่ใช่โผล่มาดังเลย")]
    [SerializeField] private float activationRadius = 15f;

    [Tooltip("เช็คระยะทุกกี่วินาที ไม่ต้องเช็คทุกเฟรม")]
    [SerializeField] private float checkInterval = 0.25f;

    private SFXHandle handle = SFXHandle.None;
    private float nextCheckTime;

    private void OnEnable()
    {
        // กระจายเวลาเช็คของแต่ละตัวไม่ให้ตรงกัน
        // ถ้า dummy 10 ตัวเช็คพร้อมกันในเฟรมเดียว จะเห็นเป็นกระตุกเป็นจังหวะ
        nextCheckTime = Time.time + Random.Range(0f, checkInterval);
    }

    private void OnDisable()
    {
        // ต้องหยุดเสมอ — PlayLoopSFXForever ไม่คืนลำโพงเข้าพูลเอง
        // ถ้าลืม object หายไปแล้วแต่เสียงยังดังอยู่ และลำโพงตัวนั้นหายจากพูลถาวร
        handle.Stop();
        handle = SFXHandle.None;
    }

    private void Start()
    {
        // OnEnable() อาจทำงานก่อน SoundManager.Awake()
        // Unity ไม่รับประกันลำดับ Awake/OnEnable ระหว่าง GameObject คนละตัว
        // Start() ทำงานหลัง Awake ของทุกตัวเสมอ จึงปลอดภัยแน่นอน
        if (!useDistanceCulling) StartLoop();
    }

    private void Update()
    {
        if (!useDistanceCulling) return;

        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        float sqrDist = AudioListenerCache.SqrDistanceToListener(transform.position);
        bool inRange = sqrDist <= activationRadius * activationRadius;

        if (inRange && !handle.IsValid)
        {
            StartLoop();
        }
        else if (!inRange && handle.IsValid)
        {
            handle.FadeOutAndStop(fadeOutTime);
            handle = SFXHandle.None;
        }
    }

    private void StartLoop()
    {
        if (loopSound == null || SoundManager.Instance == null) return;
        if (handle.IsValid) return;   // เล่นอยู่แล้ว ไม่เปิดซ้อน

        handle = SoundManager.Instance.PlayLoopSFXForever(loopSound, transform.position);
        if (!handle.IsValid) return;

        handle.SetVolumeMultiplier(0f);
        handle.FadeToVolumeMultiplier(1f, fadeInTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!useDistanceCulling) return;

        Gizmos.color = new Color(0.3f, 0.8f, 0.6f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }
#endif
}