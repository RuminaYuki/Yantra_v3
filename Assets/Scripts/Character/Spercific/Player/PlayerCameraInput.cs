using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerCameraInput : MonoBehaviour
{
    [Tooltip("ลาก PlayerCameraController มาใส่ ถ้าเว้นว่างระบบจะหาให้เอง")]
    public PlayerCameraController cameraController;

    [Header("Debug")]
    [Tooltip("ขึ้น log บอกว่าหา controller เจอจากวิธีไหน")]
    [SerializeField] private bool _logResolveMethod = false;

    private void Awake()
    {
        if (cameraController == null)
            cameraController = ResolveController();

        if (cameraController == null)
        {
            Debug.LogError(
                "[PlayerCameraInput] หา PlayerCameraController ไม่เจอ — " +
                "ลากใส่ช่อง Camera Controller เองหรือเช็คว่ามีในฉากหรือยัง", this);
            enabled = false;
        }
    }

    private PlayerCameraController ResolveController()
    {
        // 1) อยู่บน GameObject เดียวกัน
        var found = GetComponent<PlayerCameraController>();
        if (found != null) return Report(found, "GameObject เดียวกัน");

        // 2) อยู่ในลูกหลาน (รวมตัวที่ปิด active ไว้)
        found = GetComponentInChildren<PlayerCameraController>(true);
        if (found != null) return Report(found, "ลูกหลาน");

        // 3) อยู่ในสายพ่อแม่ — เคสนี้ตรงกับโครง Rin(Player) ที่ CameraProxy อยู่ลึกเข้าไป
        found = GetComponentInParent<PlayerCameraController>(true);
        if (found != null) return Report(found, "สายพ่อแม่");

        // 4) หาทั้งฉาก — ช้าสุด ใช้เป็นทางสุดท้าย
        //    FindObjectsInactive.Include เผื่อ Player ถูกปิดไว้ตอน Awake
        var all = Object.FindObjectsByType<PlayerCameraController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (all.Length == 1) return Report(all[0], "ค้นทั้งฉาก");

        if (all.Length > 1)
        {
            Debug.LogWarning(
                $"[PlayerCameraInput] เจอ PlayerCameraController {all.Length} ตัวในฉาก " +
                $"เลือกใช้ '{all[0].name}' — ควรลากใส่ช่องเองเพื่อความชัวร์", this);
            return all[0];
        }

        return null;
    }

    private PlayerCameraController Report(PlayerCameraController controller, string method)
    {
        if (_logResolveMethod)
            Debug.Log($"[PlayerCameraInput] เจอ controller จาก: {method} ({controller.name})", this);
        return controller;
    }

    private void Update()
    {
        if (cameraController == null) return;

        // ไม่ป้อน input ตอน pause หรือ cutscene
        if (cameraController.IsPaused || cameraController.IsCutsceneMode) return;

        if (Mouse.current != null)
            cameraController.FeedLookInput(Mouse.current.delta.ReadValue());
    }
}