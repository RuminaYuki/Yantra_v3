using UnityEngine;

// ป้ายบังคับเปิดกระเป๋าครั้งแรก — ขึ้นหลังปิดหน้าสอนกระเป๋า (Tutorial_Inventory) ล็อก / หน้าตาป้าย อยู่ที่ ForcedActionPrompt
// ขั้น: กด Tab เปิด (กระเป๋าเปิดผ่านระบบปกติ) → ให้ลองเล่นในกระเป๋าเงียบๆ สักพัก → ขึ้น "กด Tab ปิด" → ปิดแล้ว = จบ
public class InventoryOpenPrompt : ForcedActionPrompt
{
    private const int Open = 0, Browse = 1, Close = 2;

    [Header("ขั้นเปิดกระเป๋า")]
    [SerializeField] private string _pressText = "PRESS TO OPEN YOUR BAG";
    [Tooltip("รูปปุ่ม Tab (InputIcons_Sheet → Tab)")]
    [SerializeField] private Sprite _pressIcon;
    [SerializeField] private bool _pressBackground;

    [Header("ขั้นปิดกระเป๋า")]
    [Tooltip("เปิดกระเป๋าแล้วปล่อยให้ลองเล่นเงียบๆ กี่วินาที ก่อนขึ้นป้ายปิด (เวลาจริง)")]
    [SerializeField, Min(0f)] private float _closeHintDelay = 3f;
    [SerializeField] private string _closeText = "PRESS TO CLOSE YOUR BAG";
    [Tooltip("ว่าง = ใช้รูปเดียวกับขั้นเปิด (Tab)")]
    [SerializeField] private Sprite _closeIcon;
    [SerializeField] private bool _closeBackground;

    private InventoryUI _inventory;
    private float _browseTimer;

    protected override bool FindPlayerParts(Transform playerRoot)
    {
        // หน้ากระเป๋าอยู่ใน UI ไม่ได้อยู่ใต้ตัวผู้เล่น
        _inventory = FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include);
        return _inventory != null;
    }

    protected override void OnPromptStart() => ShowStep(Open);

    protected override void GetStepVisual(int step, out string text, out Sprite icon, out bool background)
    {
        switch (step)
        {
            case Browse:   // ช่วงลองเล่น — ป้ายว่าง ไม่รบกวน
                text = "";
                icon = null;
                background = false;
                break;
            case Close:
                text = _closeText;
                icon = _closeIcon != null ? _closeIcon : _pressIcon;
                background = _closeBackground;
                break;
            default:
                text = _pressText;
                icon = _pressIcon;
                background = _pressBackground;
                break;
        }
    }

    // กระเป๋าเปิดอยู่ InventoryUI ล็อกกล้องเอง — ไม่ปลดทับ ไม่งั้นหันกล้องได้ทั้งที่กระเป๋าเปิด (ปิดกระเป๋าแล้วมันปลดให้เอง)
    protected override bool ShouldReleaseCamera() => _inventory == null || !_inventory.IsOpen;

    protected override void OnPromptUpdate()
    {
        if (_inventory == null)
        {
            Complete();
            return;
        }

        bool open = _inventory.IsOpen;

        if (IsStep(Open))
        {
            if (open)
            {
                _browseTimer = _closeHintDelay;
                ShowStep(Browse);
            }
            return;
        }

        // ปิดกระเป๋าแล้ว (ก่อนหรือหลังป้ายปิดขึ้นก็ได้) = จบ
        if (!open)
        {
            Complete();
            return;
        }

        if (IsStep(Browse))
        {
            _browseTimer -= Time.unscaledDeltaTime;
            if (_browseTimer <= 0f)
                ShowStep(Close);
        }
    }
}
