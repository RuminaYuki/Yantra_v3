using UnityEngine;
using UnityEngine.Events;

// สั่งกำแพงหมอกทุกอันที่เป็นลูกของวัตถุนี้พร้อมกัน
// ต่อ Close / Open จาก QuestEventRelay หรือ TriggerZoneEvents ได้เลย
public class FogWallGroup : MonoBehaviour
{
    [Tooltip("เสียงหรือเอฟเฟกต์ตอนกำแพงขึ้น (เรียกครั้งเดียวต่อกลุ่ม)")]
    [SerializeField] private UnityEvent _onClosed;
    [Tooltip("เสียงหรือเอฟเฟกต์ตอนกำแพงสลาย")]
    [SerializeField] private UnityEvent _onOpened;

    public bool IsClosed { get; private set; }

    public void Close()
    {
        if (IsClosed) return;
        IsClosed = true;
        foreach (FogWall wall in GetComponentsInChildren<FogWall>(true)) wall.Close();
        _onClosed?.Invoke();
    }

    public void Open()
    {
        if (!IsClosed) return;
        IsClosed = false;
        foreach (FogWall wall in GetComponentsInChildren<FogWall>(true)) wall.Open();
        _onOpened?.Invoke();
    }

    public void SetClosedInstant(bool closed)
    {
        IsClosed = closed;
        foreach (FogWall wall in GetComponentsInChildren<FogWall>(true)) wall.SetClosedInstant(closed);
    }
}
