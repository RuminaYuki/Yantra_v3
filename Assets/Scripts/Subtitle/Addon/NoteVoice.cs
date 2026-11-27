using UnityEngine;

/// <summary>
/// เสียงอ่านโน้ต — แปะบนโน้ต แล้วให้ Interactor Event > On Interact เรียก Play()
///
///   ประโยคแรก  = เสียงอ่านโน้ต ไม่ขึ้นซับ (ตัวหนังสืออยู่บนโน้ตแล้ว)
///   ประโยคถัดไป = รินพูด ขึ้นซับตามปกติ
///   อ่านแล้ว เปิดโน้ตซ้ำ = เงียบ (นับใหม่เมื่อโหลดฉากใหม่)
///
/// เล่นผ่าน SubtitleSystem เหมือนบทพูดอื่น — ได้หรี่เพลง / หยุดตอน Pause / หยุดตอนผู้เล่นตาย ครบ
/// ข้อความเต็มยังอยู่ใน Sub_Note_... (Voice Lines / CSV ยังเห็น) แค่ตอนเล่นไม่โชว์ประโยคแรก
/// </summary>
public class NoteVoice : MonoBehaviour
{
    [Tooltip("Sub_Note_... — ประโยค 1 = เสียงอ่านโน้ต / ประโยค 2 = รินพูด")]
    [SerializeField] private SubtitleDataSO _subtitleData;

    [Tooltip("ไม่ขึ้นซับตอนเสียงอ่านโน้ต (ประโยคแรก) — ตัวหนังสืออยู่บนโน้ตแล้ว")]
    [SerializeField] private bool _hideReadingSubtitle = true;

    [Tooltip("อ่านครั้งเดียว — เปิดโน้ตซ้ำจะไม่มีเสียง")]
    [SerializeField] private bool _playOnce = true;

    /// <summary>อ่านโน้ตนี้ไปแล้วหรือยัง (ในการโหลดฉากรอบนี้)</summary>
    public bool HasBeenRead { get; private set; }

    private SubtitleDataSO _hiddenCopy;

    [ContextMenu("Play")]
    public void Play()
    {
        if (_playOnce && HasBeenRead)
            return;

        if (_subtitleData == null)
        {
            Debug.LogWarning($"[NoteVoice] {name} ยังไม่ได้ใส่ Subtitle Data", this);
            return;
        }

        if (SubtitleSystem.Instance == null)
        {
            Debug.LogWarning("[NoteVoice] ไม่มี SubtitleSystem ใน scene นี้", this);
            return;
        }

        HasBeenRead = true;
        SubtitleSystem.Instance.PlaySubtitle(_hideReadingSubtitle ? GetHiddenCopy() : _subtitleData);
    }

    [ContextMenu("Reset Read (ทดสอบ — ให้อ่านได้อีกรอบ)")]
    public void ResetRead() => HasBeenRead = false;

    // สำเนาตอนเล่น: ประโยคแรกข้อความว่าง = ไม่มีตัวหนังสือขึ้น แต่เสียงยังเล่นและรอจนจบก่อนไปประโยคริน
    private SubtitleDataSO GetHiddenCopy()
    {
        if (_hiddenCopy != null)
            return _hiddenCopy;

        SubtitleLine[] lines = _subtitleData.Lines;
        if (lines == null || lines.Length == 0)
            return _subtitleData;

        _hiddenCopy = ScriptableObject.CreateInstance<SubtitleDataSO>();
        _hiddenCopy.name = _subtitleData.name + " (ไม่ขึ้นซับตอนอ่าน)";
        _hiddenCopy.hideFlags = HideFlags.DontSave;
        _hiddenCopy.Lines = (SubtitleLine[])lines.Clone();
        _hiddenCopy.Lines[0].Speaker = null;
        _hiddenCopy.Lines[0].DefaultText = "";
        return _hiddenCopy;
    }

    private void OnDestroy()
    {
        if (_hiddenCopy != null)
            Destroy(_hiddenCopy);
    }
}
