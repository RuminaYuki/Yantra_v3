using UnityEngine;

// ป้ายบังคับร่ายเวทครั้งแรก — ขึ้นหลังปิดหน้าสอนเวท (Tut_Yan) ล็อก / หน้าตาป้าย อยู่ที่ ForcedActionPrompt
//
// ขั้น: กดคลิกขวาค้าง (วงเวทเปิด) → เลือกแล้วปล่อย (ได้ยันต์) → วาดยันต์ → คลิกซ้ายปล่อยพลัง → จบ
// ปล่อยคลิกขวาโดยไม่ได้เลือก / ยันต์หาย = กลับไปขั้นแรก
//
// ฟังแค่ event channel ของระบบเวทที่มีอยู่แล้ว ไม่ได้แก้ไฟล์ระบบเวท
public class SpellCastPrompt : ForcedActionPrompt
{
    private const int Hold = 0, Select = 1, Draw = 2, Cast = 3;

    [Header("ฟังระบบเวท (ช่องเดิมของระบบ)")]
    [Tooltip("Bool_RadialSO_EventChannel — วงเวทเปิด (true) / ปิด (false)")]
    [SerializeField] private BoolRadialSOAdvEventChannal _radialChannel;

    [Tooltip("PlayerOnFinishedSpellDraw_VEC — วาดยันต์เสร็จ")]
    [SerializeField] private VoidEventChannelSO _drawFinishedChannel;

    [Tooltip("EffectFinished_VEC — ปล่อยพลังเสร็จ = จบ")]
    [SerializeField] private VoidEventChannelSO _castFinishedChannel;

    [Tooltip("ร่ายครั้งนี้ไม่หักของ — กันผู้เล่นไม่มีวัตถุดิบแล้วค้างอยู่ขั้นแรก")]
    [SerializeField] private bool _freeCast = true;

    [Header("ข้อความ — รูปปุ่มโชว์แยกที่ Key Icon ของ SpellPrompt (UIRoot)")]
    [Tooltip("ใส่ {RMB} {LMB} ในข้อความได้ — แต่ถ้าใช้ป้ายใน UIRoot รูปปุ่มจะซ้ำกับ Key Icon")]
    [SerializeField] private string _holdText = "HOLD TO OPEN YOUR SPELLS";
    [SerializeField] private string _selectText = "MOVE TO A SPELL, THEN RELEASE";
    [SerializeField] private string _drawText = "MOVE THE MOUSE ALONG THE LINE";
    [SerializeField] private string _castText = "CLICK TO CAST";

    [Header("รูปปุ่มแต่ละขั้น / ว่าง = ไม่มีรูป")]
    [SerializeField] private Sprite _holdIcon;
    [SerializeField] private Sprite _selectIcon;
    [SerializeField] private Sprite _drawIcon;
    [SerializeField] private Sprite _castIcon;

    [Header("พื้นหลังรูปปุ่มแต่ละขั้น")]
    [SerializeField] private bool _holdBackground;
    [SerializeField] private bool _selectBackground = true;
    [SerializeField] private bool _drawBackground;
    [SerializeField] private bool _castBackground;

    private SpellController _spell;
    private int _checkTemplateFrame = -1;
    private bool _savedIgnoreCost;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_radialChannel != null) _radialChannel.Raised += HandleRadial;
        if (_drawFinishedChannel != null) _drawFinishedChannel.Raised += HandleDrawFinished;
        if (_castFinishedChannel != null) _castFinishedChannel.Raised += HandleCastFinished;
    }

    protected override void OnDisable()
    {
        if (_radialChannel != null) _radialChannel.Raised -= HandleRadial;
        if (_drawFinishedChannel != null) _drawFinishedChannel.Raised -= HandleDrawFinished;
        if (_castFinishedChannel != null) _castFinishedChannel.Raised -= HandleCastFinished;
        base.OnDisable();
    }

    protected override bool FindPlayerParts(Transform playerRoot)
    {
        _spell = playerRoot.GetComponentInChildren<SpellController>(true);
        return _spell != null;
    }

    protected override void OnPromptStart()
    {
        _checkTemplateFrame = -1;
        ShowStep(Hold);
    }

    protected override void GetStepVisual(int step, out string text, out Sprite icon, out bool background)
    {
        switch (step)
        {
            case Select: text = _selectText; icon = _selectIcon; background = _selectBackground; break;
            case Draw: text = _drawText; icon = _drawIcon; background = _drawBackground; break;
            case Cast: text = _castText; icon = _castIcon; background = _castBackground; break;
            default: text = _holdText; icon = _holdIcon; background = _holdBackground; break;
        }
    }

    protected override void OnLocked()
    {
        if (!_freeCast)
            return;
        _savedIgnoreCost = RadialMenuButtonData.IgnoreItemCost;
        RadialMenuButtonData.IgnoreItemCost = true;
    }

    protected override void OnUnlocked()
    {
        if (_freeCast)
            RadialMenuButtonData.IgnoreItemCost = _savedIgnoreCost;
    }

    // ===== ขั้นตอน (ฟังจากระบบเวท) =====

    private void HandleRadial(bool open, RadialMenuDataSO data)
    {
        if (open && IsStep(Hold))
            ShowStep(Select);
        else if (!open && IsStep(Select))
            _checkTemplateFrame = Time.frameCount + 1;   // ยันต์สร้างตอนปิดวง — รอเฟรมถัดไปค่อยเช็ค
    }

    private void HandleDrawFinished()
    {
        if (IsStep(Draw))
            ShowStep(Cast);
    }

    private void HandleCastFinished()
    {
        if (IsStep(Draw) || IsStep(Cast))
            Complete();
    }

    protected override void OnPromptUpdate()
    {
        // ปล่อยคลิกขวาแล้ว — ได้ยันต์ = ไปวาด / ไม่ได้เลือก = กลับไปกดใหม่
        if (_checkTemplateFrame >= 0 && Time.frameCount >= _checkTemplateFrame)
        {
            _checkTemplateFrame = -1;
            ShowStep(_spell != null && _spell.GetHaveTemplat ? Draw : Hold);
        }

        // ยันต์หายกลางทางโดยไม่ได้วาดเสร็จ — กลับไปเริ่มใหม่
        if (IsStep(Draw) && _spell != null && !_spell.GetHaveTemplat)
            ShowStep(Hold);
    }
}
