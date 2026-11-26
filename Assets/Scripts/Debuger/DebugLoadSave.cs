using UnityEngine;

// ปุ่ม Debug: save ที่ SpawnPoint ที่ใกล้ที่สุด / โหลด save ล่าสุดของ slot ที่เล่นอยู่ (ทิ้งทุกอย่างตั้งแต่ save ครั้งก่อน)
// SaveManager สร้างตัวเองตอนรัน ไม่ได้อยู่ในฉาก ปุ่มเลยลากใส่ OnClick ตรง ๆ ไม่ได้ — ใช้ตัวนี้เป็นตัวกลาง
// ผูก OnClick ของปุ่มใน Debug panel → DebugLoadSave.LoadLastSave
public class DebugLoadSave : MonoBehaviour
{
    [Tooltip("ว่างไว้ = หาจาก parent (Debug panel) เอง")]
    [SerializeField] private DebugPanel _panel;

    private void Awake()
    {
        if (_panel == null)
            _panel = GetComponentInParent<DebugPanel>(true);
    }

    // ปุ่ม Debug: save ที่ SpawnPoint ที่ใกล้ผู้เล่นที่สุด
    // ระบบ save จำแค่ว่าเกิดที่ SpawnPoint ตัวไหน (ไม่ได้จำตำแหน่งจริง) — โหลดแล้วจะไปโผล่ที่จุดนั้น
    // ผูก OnClick → DebugLoadSave.SaveAtNearest
    [Tooltip("ติ๊ก = save แล้วโหลดฉากใหม่เหมือน SavePoint จริง (ผีธรรมดากลับมา)\nไม่ติ๊ก = save แล้วเล่นต่อเลย")]
    [SerializeField] private bool _reloadAfterSave = false;

    public void SaveAtNearest()
    {
        SaveManager save = SaveManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[DebugLoadSave] ไม่มี SaveManager", this);
            return;
        }

        PlayerLocomotion player = PlayerLookup.Find<PlayerLocomotion>();
        if (player == null)
        {
            Debug.LogWarning("[DebugLoadSave] หาผู้เล่นไม่เจอ", this);
            return;
        }

        SpawnPoint nearest = FindNearest(player.transform.position);
        if (nearest == null)
        {
            Debug.LogWarning("[DebugLoadSave] ในฉากไม่มี SpawnPoint ที่มี SaveId", this);
            return;
        }

        if (_reloadAfterSave && _panel != null)
            _panel.Close();

        if (save.SaveAt(nearest, _reloadAfterSave))
            Debug.Log($"[DebugLoadSave] Save ที่ '{nearest.name}' (slot {save.CurrentSlot})", nearest);
        // save ไม่ผ่าน (เช่นกำลังโดนบล็อก) SaveManager log บอกเหตุผลเอง
    }

    private static SpawnPoint FindNearest(Vector3 position)
    {
        SpawnPoint best = null;
        float bestSqr = float.MaxValue;

        foreach (SpawnPoint point in FindObjectsByType<SpawnPoint>())
        {
            if (string.IsNullOrEmpty(point.Id)) continue;   // ไม่มี SaveId = โหลดแล้ววางผู้เล่นไม่ได้

            float sqr = (point.SpawnTransform.position - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = point;
            }
        }
        return best;
    }

    public void LoadLastSave()
    {
        SaveManager save = SaveManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[DebugLoadSave] ไม่มี SaveManager", this);
            return;
        }

        if (!save.HasSave)
        {
            Debug.LogWarning($"[DebugLoadSave] Slot {save.CurrentSlot} ยังไม่มี save", this);
            return;
        }

        // ปิด panel ก่อน: คืนเมาส์ / กล้อง / การเดิน / Pause ให้เหมือนเดิม
        if (_panel != null)
            _panel.Close();

        Time.timeScale = 1f;   // เผื่อมีอะไรหยุดเวลาไว้ (แบบเดียวกับปุ่ม Retry ของ GameOverScreen)
        save.LoadLastSave();
    }
}
