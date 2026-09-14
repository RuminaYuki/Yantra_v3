using UnityEngine;
using UnityEngine.InputSystem;

// ไฟล์ทดสอบชั่วคราว ลบทิ้งได้หลังระบบวาดยันต์เสร็จ
public class LockTester : MonoBehaviour
{
    private PlayerCameraController _camera;

    private void Start()
    {
        _camera = FindFirstObjectByType<PlayerCameraController>();

        if (_camera == null)
            Debug.LogError("[LockTester] หา PlayerCameraController ไม่เจอ");
    }

    private void Update()
    {
        if (_camera == null) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            _camera.IsLookLocked = !_camera.IsLookLocked;
            Debug.Log("Camera Locked = " + _camera.IsLookLocked);
        }
    }
}