using UnityEngine;
using System.Collections.Generic;

public class LevelAudioManager : MonoBehaviour
{
    public static LevelAudioManager Instance { get; private set; }

    // ตัวแปรสำหรับล็อคกล่องเสียงตอนคัตซีนทำงาน
    public static bool IsCutsceneActive = false;

    [Header("เพลงประกอบฉาก (BGM)")]
    [Tooltip("เพลงประจำฉากนี้\n" +
        "ถ้าฉากมี BattleMusicController มันจะเอาเพลงนี้ไปใช้เป็นเพลงตอนไม่รบให้เอง")]
    [SerializeField] private SoundID sceneBGM;

    [Header("Debug")]
    [Tooltip("เปิดเพื่อดูว่าใครเป็นคนสั่งหรี่เสียง ambient — ปิดเมื่อหาเจอแล้ว")]
    [SerializeField] private bool logMuffleCalls = false;

    [Header("Cutscene Ducking")]
    [Tooltip("ตัวคูณความดัง ambient ระหว่างคัตซีน (0.4 = เหลือ 40%)" +
        "\nหรี่ลงเพื่อให้บทพูดกับเสียงในคัตซีนเด่นขึ้น")]
    [Range(0f, 1f)]
    [SerializeField] private float cutsceneAmbientMultiplier = 0.4f;

    [SerializeField] private float cutsceneFadeTime = 1f;

    [Header("เสียงบรรยากาศภายนอก (Outside Ambient)")]
    [SerializeField] private SoundID[] outsideAmbientSounds;

    // เก็บ "ใบเสร็จ" แทนตัวลำโพงจริง
    private List<SFXHandle> activeOutsideAmbients = new List<SFXHandle>();

    // แยกความดังเป็น 2 ชั้นคูณกัน
    //   zoneMultiplier     = เข้า/ออกบ้าน (สั่งจาก AmbientZoneTrigger)
    //   cutsceneMultiplier = หรี่ตอนคัตซีน
    // ถ้าเขียนทับกันตรงๆ พอคัตซีนจบ ambient จะดันกลับเป็น 1.0
    // ทั้งที่ตัวละครยังอยู่ในบ้าน — หลักการเดียวกับ crossfade x duck ใน BGM
    private float zoneMultiplier = 1f;
    private float cutsceneMultiplier = 1f;

    /// <summary>
    /// เพลงประจำฉาก — BattleMusicController มาอ่านไปใช้เป็นเพลงตอนไม่รบ
    /// ถ้าช่อง Idle Music ของมันเว้นว่างไว้
    /// </summary>
    public SoundID SceneBGM => sceneBGM;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        // เคลียร์ static ตอนเปลี่ยนฉาก
        // ไม่งั้นฉากใหม่จะเห็น Instance ค้างเป็นตัวเก่าที่ถูก Destroy ไปแล้ว
        if (Instance == this) Instance = null;
    }

    // ==========================================
    // เชื่อมต่อกับระบบ Cutscene
    // ==========================================
    private void OnEnable()
    {
        CutsceneController.OnGlobalCutsceneStateChanged += HandleCutsceneState;
    }

    private void OnDisable()
    {
        CutsceneController.OnGlobalCutsceneStateChanged -= HandleCutsceneState;

        // [FIX] MEMORY LEAK — ambient ที่เปิดด้วย PlayLoopSFXForever ไม่คืนลำโพงเอง
        // ตอนเปลี่ยนฉาก ลำโพงพวกนี้จะค้างอยู่ตลอดกาล กินโควต้าพูลถาวร
        // ฉากไหนมี ambient 3 ตัว เปลี่ยนฉากไปมา 5 รอบ = เสียลำโพงถาวร 15 ตัว
        for (int i = 0; i < activeOutsideAmbients.Count; i++)
            activeOutsideAmbients[i].Stop();

        activeOutsideAmbients.Clear();
    }

    private void HandleCutsceneState(bool isPlaying)
    {
        IsCutsceneActive = isPlaying;

        cutsceneMultiplier = isPlaying ? cutsceneAmbientMultiplier : 1f;
        ApplyAmbientVolume(cutsceneFadeTime);
    }

    private void Start()
    {
        if (SoundManager.Instance == null) return;

        StartSceneBgm();
        StartOutsideAmbients();
    }

    /// <summary>
    /// เปิดเพลงประจำฉาก — ยกเว้นเมื่อมีคนอื่นคุมเพลงอยู่แล้ว
    /// </summary>
    private void StartSceneBgm()
    {
        if (sceneBGM == null) return;

        // ฉากที่มีระบบรบ ปล่อยให้ BattleMusicController คุมเพลงคนเดียว
        // มันจะหยิบ sceneBGM ไปใช้เป็นเพลงตอนไม่รบเองผ่าน property SceneBGM
        if (HasBattleMusicController())
        {
#if UNITY_EDITOR
            Debug.Log("[LevelAudio] ฉากนี้มี BattleMusicController — ส่งต่อให้มันคุมเพลงแทน\n" +
                $"'{sceneBGM.name}' จะถูกใช้เป็นเพลงตอนไม่รบ", this);
#endif
            return;
        }

        SoundManager.Instance.PlayBGM(sceneBGM);
    }

    private void StartOutsideAmbients()
    {
        if (outsideAmbientSounds == null) return;

        foreach (var ambient in outsideAmbientSounds)
        {
            if (ambient == null) continue;

            SFXHandle handle = SoundManager.Instance.PlayLoopSFXForever(ambient, transform.position);

            // เช็คใบเสร็จว่าใช้ได้จริงก่อนเก็บ
            if (handle.IsValid) activeOutsideAmbients.Add(handle);
        }
    }

    private bool HasBattleMusicController()
    {
        // เรียกครั้งเดียวตอนเริ่มฉาก ไม่ได้อยู่ใน Update เลยไม่กระทบเฟรมเรต
        // Exclude = ไม่นับตัวที่ปิด active ไว้ เพราะมันจะไม่ทำงานอยู่แล้ว
        return Object.FindAnyObjectByType<BattleMusicController>(FindObjectsInactive.Exclude) != null;
    }

    public void MuffleOutsideAmbients(float targetMultiplier, float fadeTime)
    {
#if UNITY_EDITOR
        if (logMuffleCalls)
        {
            // StackTrace จะบอกว่าใครเรียกฟังก์ชันนี้ ไล่ดูบรรทัดที่ 2-3 จากบนจะเจอตัวการ
            Debug.Log($"[Muffle] หรี่เป็น {targetMultiplier} ใน {fadeTime} วิ | เฟรม {Time.frameCount}\n"
                + System.Environment.StackTrace);
        }
#endif

        zoneMultiplier = targetMultiplier;
        ApplyAmbientVolume(fadeTime);
    }

    private void ApplyAmbientVolume(float fadeTime)
    {
        float finalMultiplier = zoneMultiplier * cutsceneMultiplier;

        // เดินถอยหลัง เพื่อลบใบเสร็จหมดอายุออกจาก list ได้อย่างปลอดภัย
        for (int i = activeOutsideAmbients.Count - 1; i >= 0; i--)
        {
            SFXHandle handle = activeOutsideAmbients[i];

            if (!handle.IsValid)
            {
                activeOutsideAmbients.RemoveAt(i);
                continue;
            }

            handle.FadeToVolumeMultiplier(finalMultiplier, fadeTime);
        }
    }
}