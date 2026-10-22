using UnityEngine;

// กล่องเดินชนแล้วขึ้น tutorial — เห็นครั้งเดียวต่อการเปิดเกม (Retry แล้วไม่ขึ้นซ้ำ)
[RequireComponent(typeof(Collider))]
public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private TutorialDataSO _tutorial;

    public void OnEnter(GameObject other)
    {
        if (_tutorial == null)
        {
            Debug.LogWarning($"[TutorialTrigger] {name} ยังไม่ได้ใส่ Tutorial", this);
            return;
        }

        // ถ้าเคยเห็นแล้วก็ไม่ต้องขึ้นซ้ำ
        if (TutorialScreen.HasSeen(_tutorial))
            return;

        // ขึ้น Tutorial
        TutorialScreen.TryShow(_tutorial);
    }
}