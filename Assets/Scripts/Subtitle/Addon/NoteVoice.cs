using System.Collections;
using SDFcl.GamePlay.Interactable;
using UnityEngine;
using Yantra.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// เสียงอ่านโน้ต — แปะบนโน้ต แล้วให้ Interactor Event > On Interact เรียก Play()
///
///   ประโยคแรก  = เสียงอ่านโน้ต — เล่นเสียงตรง ๆ ไม่ผ่านระบบซับ เลยไม่มีกล่องซับโผล่เลย (ตัวหนังสืออยู่บนโน้ตแล้ว)
///   ประโยคถัดไป = รินพูด — ส่งเข้า SubtitleSystem ขึ้นซับตามปกติ
///   อ่านครั้งเดียว — เปิดโน้ตซ้ำ = เงียบ (นับใหม่เมื่อโหลดฉากใหม่)
///   ข้ามได้ — ปิดโน้ต หรือกดปุ่มข้าม ระหว่างเสียงอ่าน
///
/// เสียงอ่านใช้ SoundManager.PlayVoice ตัวเดียวกับบทพูด — ได้ Voice mixer + หรี่เพลง + หยุดตอน Pause ครบ
/// </summary>
public class NoteVoice : MonoBehaviour
{
    [Tooltip("Sub_Note_... — ประโยค 1 = เสียงอ่านโน้ต / ประโยค 2 ขึ้นไป = รินพูด")]
    [SerializeField] private SubtitleDataSO _subtitleData;

    [Tooltip("อ่านครั้งเดียว — เปิดโน้ตซ้ำจะไม่มีเสียง")]
    [SerializeField] private bool _playOnce = true;

    [Header("ข้าม")]
    [Tooltip("ปิดโน้ตระหว่างเสียงอ่าน = หยุดเสียงอ่าน")]
    [SerializeField] private bool _skipOnClose = true;

    [Tooltip("กดปุ่มข้ามได้ — Space / Enter / ปุ่ม A ของจอย\nตอนเสียงอ่าน = ข้ามเสียงอ่าน / ตอนรินพูด = ข้ามประโยค")]
    [SerializeField] private bool _skipWithKey = true;

    [Tooltip("ข้ามเสียงอ่านแล้ว รินยังพูดต่อ — เอาติ๊กออก = ข้ามแล้วเงียบไปเลย")]
    [SerializeField] private bool _rinTalksAfterSkip = true;

    [Tooltip("เสียงอ่านจบแล้ว เว้นจังหวะกี่วินาทีก่อนรินพูด")]
    [SerializeField, Min(0f)] private float _gapBeforeRin = 0.3f;

    /// <summary>อ่านโน้ตนี้ไปแล้วหรือยัง (ในการโหลดฉากรอบนี้)</summary>
    public bool HasBeenRead { get; private set; }

    /// <summary>กำลังเล่นเสียงอ่านอยู่</summary>
    public bool IsReading => _reading != null;

    private Iinteractor _interactor;
    private Coroutine _reading;
    private bool _skipRequested;
    private bool _rinTalking;
    private SubtitleDataSO _rinOnly;

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        if (_interactor != null) _interactor.OnEndInteract += HandleClose;
    }

    private void OnDisable()
    {
        if (_interactor != null) _interactor.OnEndInteract -= HandleClose;
        if (_reading != null) StopReading();
    }

    private void OnDestroy()
    {
        if (_rinOnly != null) Destroy(_rinOnly);
    }

    [ContextMenu("Play")]
    public void Play()
    {
        if (_playOnce && HasBeenRead) return;
        if (_reading != null) return;

        if (_subtitleData == null || _subtitleData.Lines == null || _subtitleData.Lines.Length == 0)
        {
            Debug.LogWarning($"[NoteVoice] {name} ยังไม่ได้ใส่ Subtitle Data", this);
            return;
        }

        HasBeenRead = true;
        _reading = StartCoroutine(ReadRoutine());
    }

    /// <summary>ข้ามเสียงอ่าน (ถ้ากำลังอ่านอยู่)</summary>
    public void Skip()
    {
        if (_reading != null) _skipRequested = true;
    }

    [ContextMenu("Reset Read (ทดสอบ — ให้อ่านได้อีกรอบ)")]
    public void ResetRead() => HasBeenRead = false;

    private void Update()
    {
        if (!_skipWithKey || !SkipKeyPressed()) return;

        if (_reading != null)
            _skipRequested = true;
        else if (_rinTalking && SubtitleSystem.Instance != null && SubtitleSystem.Instance.IsPlaying)
            SubtitleSystem.Instance.SkipLine();
    }

    private void HandleClose(GameObject rootPlayer)
    {
        if (_skipOnClose) Skip();
    }

    // ---------- เล่น ----------

    private IEnumerator ReadRoutine()
    {
        _skipRequested = false;
        SoundManager sound = SoundManager.Instance;
        AudioClip clip = _subtitleData.Lines[0].VoiceClip;

        // ตัดบทพูดอื่นที่ค้างอยู่ — ใช้ลำโพงเสียงพากย์ตัวเดียวกัน
        if (SubtitleSystem.Instance != null && SubtitleSystem.Instance.IsPlaying)
            SubtitleSystem.Instance.StopSubtitle();

        bool skipped = false;

        if (clip != null && sound != null)
        {
            float length = sound.PlayVoice(clip);
            float elapsed = 0f;
            bool held = false;

            while (elapsed < length)
            {
                if (_skipRequested) { skipped = true; break; }

                // เปิดเมนู / Pause = หยุดเสียงไว้ก่อน เหมือนบทพูดปกติ
                bool hold = ShouldHold();
                if (hold != held)
                {
                    held = hold;
                    if (hold) sound.PauseVoice();
                    else sound.ResumeVoice();
                }

                if (!hold) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (skipped || held) sound.StopVoice();
        }

        _reading = null;
        _skipRequested = false;

        if (skipped && !_rinTalksAfterSkip) yield break;
        if (_subtitleData.Lines.Length < 2 || SubtitleSystem.Instance == null) yield break;

        if (!skipped && _gapBeforeRin > 0f)
            yield return new WaitForSecondsRealtime(_gapBeforeRin);

        SubtitleSystem.Instance.PlaySubtitle(GetRinOnly());
        _rinTalking = true;
        while (SubtitleSystem.Instance != null && SubtitleSystem.Instance.IsPlaying)
            yield return null;
        _rinTalking = false;
    }

    private void StopReading()
    {
        StopAllCoroutines();
        _reading = null;
        _skipRequested = false;
        _rinTalking = false;
        if (SoundManager.Instance != null) SoundManager.Instance.StopVoice();
    }

    // สำเนาตอนเล่น: ตัดประโยคแรก (เสียงอ่าน) ออก เหลือแต่ประโยคที่รินพูด
    private SubtitleDataSO GetRinOnly()
    {
        if (_rinOnly != null) return _rinOnly;

        SubtitleLine[] lines = _subtitleData.Lines;
        _rinOnly = ScriptableObject.CreateInstance<SubtitleDataSO>();
        _rinOnly.name = _subtitleData.name + " (รินพูด)";
        _rinOnly.hideFlags = HideFlags.DontSave;
        _rinOnly.Lines = new SubtitleLine[lines.Length - 1];
        System.Array.Copy(lines, 1, _rinOnly.Lines, 0, lines.Length - 1);
        return _rinOnly;
    }

    private static bool ShouldHold()
    {
        PauseController pause = PauseController.Instance;
        if (pause != null && pause.IsPaused) return true;

        UIManager ui = UIManager.Instance;
        return ui != null && ui.HasAnyOpen;
    }

    private static bool SkipKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        return (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) ||
               (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
    }
}
