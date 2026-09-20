using UnityEngine;
using UnityEngine.InputSystem;

// ไฟล์ทดสอบชั่วคราว ลบทิ้งได้หลังต่อระบบ HP เสร็จ
public class ShakeTester : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            Debug.Log("กด H แล้ว / Shaker = " + (CameraShaker.Instance != null));
            CameraShaker.Instance?.AddTrauma(0.5f);
        }
    }
}