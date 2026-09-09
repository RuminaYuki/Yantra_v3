using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraInput : MonoBehaviour
{
    [Tooltip("ลากกล้อง PlayerCameraController มาใส่ช่องนี้ ถ้าเว้นว่างไว้ ระบบจะหาให้อัตโนมัติ")]
    public PlayerCameraController cameraController;

    private void Start()
    {
        if (cameraController == null)
        {
            if (Camera.main != null)
            {
                cameraController = Camera.main.GetComponent<PlayerCameraController>();
            }

            if (cameraController == null)
            {
                Debug.LogWarning("PlayerCameraInput: หา PlayerCameraController ไม่เจอ...");
            }
        }
    }

    private void Update()
    {
        if (cameraController == null) return;

        // 1. เช็คว่าผู้เล่นกดปุ่ม ESC ในเฟรมนี้หรือเปล่า?
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // สลับโหมด Pause (ถ้าเปิดอยู่ให้ปิด / ถ้าปิดอยู่ให้เปิด)
            cameraController.IsPaused = !cameraController.IsPaused;
        }

        // 2. ถ้าเกม "ไม่ได้" Pause อยู่ ถึงจะยอมส่งค่าเมาส์ไปให้กล้องหมุน
        if (!cameraController.IsPaused && Mouse.current != null)
        {
            cameraController.FeedLookInput(Mouse.current.delta.ReadValue());
        }
    }
}