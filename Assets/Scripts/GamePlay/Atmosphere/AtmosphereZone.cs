using UnityEngine;

// โซนเปลี่ยนบรรยากาศ (แสง + หมอก) — ผู้เล่นเดินเข้า = ค่อยๆ เปลี่ยนเป็น Profile ที่ใส่ไว้
// ต่อกับ TriggerZone เอง ไม่ต้องผูก UnityEvent / ไม่ต้องลาก AtmosphereController (หาในฉากเอง)
//
// วิธีใช้: GameObject เปล่า + Box Collider (Is Trigger) + TriggerZone + สคริปต์นี้ แล้วใส่ Profile
// รูปร่างซับซ้อน: ใส่ Box Collider หลายตัวบน GameObject เดียวกันได้ (ติ๊ก Is Trigger ทุกตัว) — นับเป็นโซนเดียว
//
// โซนซ้อนกันได้: AtmosphereController จำว่าผู้เล่นอยู่ในโซนไหนบ้าง
//   - ใช้โซนที่ Priority (ตั้งใน Enter Profile) สูงสุด / เท่ากัน = โซนที่เข้าล่าสุด
//   - ออกจากโซนหนึ่ง → กลับไปใช้โซนที่ยังอยู่ข้างในเอง
//   - Exit Profile ใช้เฉพาะตอนออกแล้วไม่ได้อยู่ในโซนไหนเลย
[RequireComponent(typeof(TriggerZone))]
public class AtmosphereZone : MonoBehaviour
{
    [Tooltip("บรรยากาศตอนอยู่ในโซนนี้")]
    [SerializeField] private AtmosphereProfileSO _enterProfile;

    [Tooltip("บรรยากาศตอนเดินออกแล้วไม่ได้อยู่ในโซนไหนเลย — เว้นว่าง = ค้างค่าเดิม\n" +
             "ถ้ายังอยู่ในโซนอื่น จะใช้ Profile ของโซนนั้นแทน (ช่องนี้ไม่ถูกใช้)")]
    [SerializeField] private AtmosphereProfileSO _exitProfile;

    [Tooltip("เติมเองตอนใส่สคริปต์ / วางในฉาก — ถ้ายังว่าง ตอนเล่นจะหาในฉากให้อีกรอบ")]
    [SerializeField] private AtmosphereController _controller;

    private TriggerZone _zone;

    public AtmosphereProfileSO EnterProfile => _enterProfile;

    // Priority อยู่ใน Profile — ตั้งครั้งเดียว ทุกโซนที่ใช้ Profile นั้นได้ค่าเดียวกัน
    public int Priority => _enterProfile != null ? _enterProfile.priority : 0;

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

        // ถูกปิดระหว่างผู้เล่นอยู่ข้างใน → เอาตัวเองออกจากรายการ ไม่งั้นค้างคุมบรรยากาศทั้งที่ปิดไปแล้ว
        // (TriggerZone ก็ยิง Exit ตอนถูกปิด แต่ลำดับ OnDisable ระหว่าง component ไม่แน่นอน
        //  อาจยกเลิก subscribe ไปก่อนได้ยิน — เรียกซ้ำได้ ไม่อยู่ในรายการแล้วก็ไม่ทำอะไร)
        if (_controller != null) _controller.ExitZone(this);
    }

    private void HandleEnter(GameObject who)
    {
        AtmosphereController c = GetController();
        if (c != null) c.EnterZone(this);
    }

    private void HandleExit(GameObject who)
    {
        // ยังมี object อื่นที่ผ่าน filter อยู่ในโซน = ยังไม่ออกจริง
        if (_zone.IsOccupied) return;

        AtmosphereController c = GetController();
        if (c == null) return;

        // ยังอยู่ในโซนอื่น → controller กลับไปใช้โซนนั้นเอง
        bool stillInOtherZone = c.ExitZone(this);

        // ไม่อยู่ในโซนไหนแล้ว → ใช้ Exit Profile (ถ้ามี)
        if (!stillInOtherZone && _exitProfile != null && c.CurrentProfile != _exitProfile)
            c.ApplyProfile(_exitProfile);
    }

    private AtmosphereController GetController()
    {
        if (_controller == null) _controller = FindAnyObjectByType<AtmosphereController>();
        if (_controller == null)
            Debug.LogWarning($"[AtmosphereZone] {name}: ไม่มี AtmosphereController ในฉาก — ใส่ไว้ที่ Directional Light", this);
        return _controller;
    }
}
