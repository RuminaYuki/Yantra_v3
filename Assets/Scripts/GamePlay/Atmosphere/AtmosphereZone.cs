using UnityEngine;

// โซนเปลี่ยนบรรยากาศ (แสง + หมอก) — ผู้เล่นเดินเข้า = ค่อยๆ เปลี่ยนเป็น Profile ที่ใส่ไว้
// ต่อกับ TriggerZone เอง ไม่ต้องผูก UnityEvent / ไม่ต้องลาก AtmosphereController (หาในฉากเอง)
//
// วิธีใช้: GameObject เปล่า + Box Collider (Is Trigger) + TriggerZone + สคริปต์นี้ แล้วใส่ Profile
// ทางเชื่อมระหว่าง 2 พื้นที่ (เช่น ป่า ↔ หมู่บ้าน): วางโซนคลุมแต่ละพื้นที่ หรือวางโซนบางๆ 2 อันตรงทางเข้าแต่ละฝั่ง
[RequireComponent(typeof(TriggerZone))]
public class AtmosphereZone : MonoBehaviour
{
    [Tooltip("บรรยากาศตอนเดินเข้าโซน")]
    [SerializeField] private AtmosphereProfileSO _enterProfile;

    [Tooltip("บรรยากาศตอนเดินออกจากโซน — เว้นว่าง = ไม่ทำอะไร (ค้างเป็นของโซนนี้)")]
    [SerializeField] private AtmosphereProfileSO _exitProfile;

    [Tooltip("เติมเองตอนใส่สคริปต์ / วางในฉาก — ถ้ายังว่าง ตอนเล่นจะหาในฉากให้อีกรอบ")]
    [SerializeField] private AtmosphereController _controller;

    private TriggerZone _zone;

    private void Awake() => _zone = GetComponent<TriggerZone>();

#if UNITY_EDITOR
    // ใส่สคริปต์ / แก้ค่าใน Inspector → เติมช่อง Controller ให้เอง
    private void Reset() => FillController();
    private void OnValidate() => FillController();

    private void FillController()
    {
        if (_controller != null || Application.isPlaying) return;

        // prefab ในโฟลเดอร์ Project อ้าง object ในฉากไม่ได้ — ปล่อยว่าง ไปหาตอนเล่นแทน
        if (!gameObject.scene.IsValid()) return;

        foreach (AtmosphereController c in FindObjectsByType<AtmosphereController>(FindObjectsInactive.Include))
        {
            if (c.gameObject.scene != gameObject.scene) continue;
            _controller = c;
            UnityEditor.EditorUtility.SetDirty(this);
            return;
        }
    }
#endif

    private void OnEnable()
    {
        _zone.OnZoneEnter += HandleEnter;
        _zone.OnZoneExit += HandleExit;
    }

    private void OnDisable()
    {
        _zone.OnZoneEnter -= HandleEnter;
        _zone.OnZoneExit -= HandleExit;
    }

    private void HandleEnter(GameObject who) => Apply(_enterProfile);

    private void HandleExit(GameObject who)
    {
        if (_exitProfile != null) Apply(_exitProfile);
    }

    private void Apply(AtmosphereProfileSO profile)
    {
        if (profile == null) return;

        if (_controller == null) _controller = FindAnyObjectByType<AtmosphereController>();
        if (_controller == null)
        {
            Debug.LogWarning($"[AtmosphereZone] {name}: ไม่มี AtmosphereController ในฉาก — ใส่ไว้ที่ Directional Light", this);
            return;
        }

        // อยู่ใน Profile นี้อยู่แล้ว ไม่ต้องเริ่มเฟดใหม่
        if (_controller.CurrentProfile == profile) return;

        _controller.ApplyProfile(profile);
    }
}
