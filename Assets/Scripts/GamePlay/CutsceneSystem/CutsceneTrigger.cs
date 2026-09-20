using UnityEngine;

public class CutsceneTrigger : MonoBehaviour
{
    [Tooltip("ลาก Cutscene Manager (ที่มี Timeline) มาใส่")]
    public CutsceneController cutsceneManager;

    private void OnTriggerEnter(Collider other)
    {
        // เช็คว่าคนที่เดินมาชนคือ Player ใช่ไหม
        if (other.CompareTag("Player"))
        {
            if (cutsceneManager != null)
            {
                cutsceneManager.PlayCutscene(); // สั่งเล่นคัทซีน!
            }

            // ปิดกล่องนี้ทิ้ง จะได้ไม่เดินชนซ้ำสอง
            gameObject.SetActive(false);
        }
    }
}