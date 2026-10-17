using UnityEngine;

// กล่องเดินชนแล้วขึ้น tutorial — เห็นครั้งเดียวต่อการเปิดเกม (Retry แล้วไม่ขึ้นซ้ำ)
[RequireComponent(typeof(Collider))]
public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private TutorialDataSO _tutorial;

    [Tooltip("tag ของผู้เล่น — เช็คไล่ขึ้นไปทางตัวแม่ด้วย เพราะใน FPP collider ที่ชนจริงอยู่ที่ตัวลูก")]
    [SerializeField] private string _playerTag = "Player";

    // ทำงานตอน Add Component — ตั้ง Is Trigger ให้เลย
    private void Reset()
    {
        if (TryGetComponent(out Collider col))
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (_tutorial == null)
        {
            Debug.LogWarning($"[TutorialTrigger] {name} ยังไม่ได้ใส่ Tutorial", this);
            return;
        }

        // เคยเห็นแล้ว (เช่นตายแล้ว Retry) ก็ปิดทิ้งเหมือนกัน
        if (TutorialScreen.TryShow(_tutorial) || TutorialScreen.HasSeen(_tutorial))
            gameObject.SetActive(false);
    }

    private bool IsPlayer(Collider other)
    {
        for (Transform t = other.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(_playerTag))
                return true;
        }

        return false;
    }
}