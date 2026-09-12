using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// เสียง UI — วางตัวเดียวบน UIRoot
///
/// มันจะไล่หา Selectable ทุกตัวใต้ UIRoot แล้วแปะตัวรับ event ให้เอง
/// ไม่ต้องไปแตะปุ่มทีละอัน และปุ่มที่เพิ่มทีหลังก็แค่กด Rescan
///
/// ส่งเสียงผ่าน SoundManager เสมอ เพื่อให้ mixer routing กับ slider ทำงานถูกต้อง
/// </summary>
public class UISoundBank : MonoBehaviour
{
    public static UISoundBank Instance { get; private set; }

    [Header("Sound IDs")]
    [SerializeField] private SoundID _hover;
    [SerializeField] private SoundID _click;
    [SerializeField] private SoundID _back;

    [Header("Options")]
    [Tooltip("หน่วงหลังเปิดหน้าใหม่ ก่อนยอมให้เสียง hover ดัง\n" +
             "กันเสียงลั่นตอน UIScreen สั่ง SetSelectedGameObject ให้ปุ่มแรกอัตโนมัติ")]
    [SerializeField] private float _selectGraceTime = 0.15f;

    [Tooltip("ระยะเวลาขั้นต่ำระหว่างเสียง hover สองครั้ง กันเสียงรัวตอนลากเมาส์ผ่านเร็ว ๆ")]
    [SerializeField] private float _hoverCooldown = 0.04f;

    private float _lastHoverTime = -1f;
    private float _lastRescanTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        Rescan();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// แปะตัวรับ event ให้ Selectable ทุกตัวใต้ UIRoot
    /// include inactive ด้วย เพราะหน้าจอส่วนใหญ่ปิดอยู่ตอน Awake
    /// </summary>
    [ContextMenu("Rescan Buttons")]
    public void Rescan()
    {
        _lastRescanTime = Time.unscaledTime;

        var selectables = GetComponentsInChildren<Selectable>(true);
        int added = 0;

        foreach (var selectable in selectables)
        {
            if (selectable.GetComponent<UISoundEmitter>() != null) continue;
            selectable.gameObject.AddComponent<UISoundEmitter>();
            added++;
        }

        Debug.Log($"[UISoundBank] แปะเสียงให้ {added} ปุ่ม (ทั้งหมด {selectables.Length})", this);
    }

    // ---------- เรียกโดย UISoundEmitter ----------

    internal void PlayHover(bool fromSelectEvent)
    {
        // ตอนเปิดหน้าใหม่ UIScreen จะสั่งเลือกปุ่มแรกให้อัตโนมัติ
        // ถ้าไม่กันไว้ จะมีเสียง hover ลั่นทุกครั้งที่เปิดหน้า
        if (fromSelectEvent && Time.unscaledTime - _lastRescanTime < _selectGraceTime) return;

        if (Time.unscaledTime - _lastHoverTime < _hoverCooldown) return;
        _lastHoverTime = Time.unscaledTime;

        Play(_hover);
    }

    internal void PlayClick() => Play(_click);
    public void PlayBack() => Play(_back);

    /// <summary>ให้หน้าจอเรียกตอนเปิดใหม่ เพื่อรีเซ็ตช่วงกันเสียงลั่น</summary>
    public void NotifyScreenOpened()
    {
        _lastRescanTime = Time.unscaledTime;
    }

    private void Play(SoundID id)
    {
        if (id == null) return;

        var sm = SoundManager.Instance;
        if (sm == null) return;

        sm.PlayEventSFX(id);
    }
}

/// <summary>
/// ตัวรับ event ต่อปุ่ม — ถูก AddComponent โดย UISoundBank ไม่ต้องแปะเอง
///
/// ใช้ OnPointerDown ไม่ใช่ OnPointerClick
/// เพราะปุ่มที่กดแล้วเปลี่ยนหน้าจอ จะปิด CanvasGroup.interactable ทันทีใน onClick
/// ถ้ารอถึง OnPointerClick จะเช็คเจอว่ากดไม่ได้แล้ว แล้วเงียบไปเฉย ๆ
/// </summary>
public class UISoundEmitter : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, ISubmitHandler,
    ISelectHandler
{
    private bool _pointerInside;

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        if (!IsUsable()) return;
        UISoundBank.Instance?.PlayHover(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!IsUsable()) return;

        // เมาส์อยู่บนปุ่มนี้อยู่แล้ว = hover ดังไปแล้ว ไม่ต้องซ้ำ
        // OnSelect มีไว้สำหรับจอย/คีย์บอร์ดเท่านั้น
        if (_pointerInside) return;

        UISoundBank.Instance?.PlayHover(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsUsable()) return;
        UISoundBank.Instance?.PlayClick();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!IsUsable()) return;
        UISoundBank.Instance?.PlayClick();
    }

    /// <summary>ปุ่มที่กดไม่ได้ ไม่ควรมีเสียงตอบสนอง</summary>
    private bool IsUsable()
    {
        var selectable = GetComponent<Selectable>();
        return selectable != null && selectable.IsInteractable();
    }
}