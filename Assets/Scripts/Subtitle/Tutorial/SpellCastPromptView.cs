using TMPro;
using UnityEngine;
using UnityEngine.UI;

// หน้าตาป้ายบังคับร่ายเวท (ทำไว้ใน UIRoot ข้างๆ BlockQTEPrompt) — เก็บแค่ชิ้น UI ไม่มี logic
// SpellCastPrompt หาตัวนี้เอง แล้วคุมข้อความ / รูปปุ่ม (ตั้งที่ SpellCastPrompt) / เต้นตุบๆ ให้
// ไม่มีตัวนี้ในฉาก = SpellCastPrompt สร้างป้ายตัวหนังสือเองแทน
public class SpellCastPromptView : MonoBehaviour
{
    [Tooltip("CanvasGroup ของทั้งก้อน — ใช้ซ่อน / โชว์")]
    [SerializeField] private CanvasGroup _group;

    [Tooltip("รูปปุ่มเมาส์ — เต้นตุบๆ ตอนรอกด")]
    [SerializeField] private Image _key;

    [SerializeField] private TMP_Text _label;

    [Tooltip("พื้นหลังวงรีขอบจางหลังรูปปุ่ม — สี / ความเข้มปรับที่ Color ของ Image นี้ / ขนาดปรับที่ SpellCastPrompt")]
    [SerializeField] private Image _keyBackground;

    // รูปปุ่มแต่ละขั้นตั้งที่ SpellCastPrompt (Hold Icon / Select Icon / ...)
    public CanvasGroup Group => _group;
    public Image Key => _key;
    public TMP_Text Label => _label;
    public Image KeyBackground => _keyBackground;

    private void Awake()
    {
        if (_group == null)
            _group = GetComponent<CanvasGroup>();
        if (_group != null)
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }
    }
}
