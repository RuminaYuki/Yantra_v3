using UnityEngine;

// ป้ายบังคับเปิดกระเป๋าครั้งแรก — ขึ้นหลังปิดหน้าสอนกระเป๋า (Tutorial_Inventory) ล็อก / หน้าตาป้าย อยู่ที่ ForcedActionPrompt
// ผู้เล่นกด Tab เอง กระเป๋าเปิดผ่านระบบปกติ (Inventory_State) → กระเป๋าเปิดแล้ว = จบ
public class InventoryOpenPrompt : ForcedActionPrompt
{
    [Header("ข้อความ / รูปปุ่ม")]
    [SerializeField] private string _pressText = "PRESS TO OPEN YOUR BAG";
    [Tooltip("รูปปุ่ม Tab (InputIcons_Sheet → Tab)")]
    [SerializeField] private Sprite _pressIcon;
    [SerializeField] private bool _pressBackground;

    private InventoryUI _inventory;

    protected override bool FindPlayerParts(Transform playerRoot)
    {
        // หน้ากระเป๋าอยู่ใน UI ไม่ได้อยู่ใต้ตัวผู้เล่น
        _inventory = FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include);
        return _inventory != null;
    }

    protected override void OnPromptStart() => ShowStep(0);

    protected override void GetStepVisual(int step, out string text, out Sprite icon, out bool background)
    {
        text = _pressText;
        icon = _pressIcon;
        background = _pressBackground;
    }

    // กระเป๋าเปิดแล้ว InventoryUI ล็อกกล้องเองอยู่ — ไม่ปลดทับ ไม่งั้นหันกล้องได้ทั้งที่กระเป๋าเปิด (ปิดกระเป๋าแล้วมันปลดให้เอง)
    protected override bool ShouldReleaseCamera() => _inventory == null || !_inventory.IsOpen;

    protected override void OnPromptUpdate()
    {
        if (_inventory != null && _inventory.IsOpen)
            Complete();
    }
}
