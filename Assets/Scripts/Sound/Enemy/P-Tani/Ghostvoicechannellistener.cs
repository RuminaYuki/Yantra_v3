using UnityEngine;

/// <summary>
/// ตัวเชื่อมระหว่าง Event Channel ของทีมกับระบบเสียงผี
///
/// ทำไมแยกเป็นไฟล์ต่างหาก:
/// GhostSoundBase ไม่ควรรู้จัก VoidEventChannelSO เลย มันควรรู้แค่เรื่องเสียง
/// ตัวนี้ทำหน้าที่ "แปลสัญญาณ" อย่างเดียว — รับ event เข้ามา แล้วเรียก API เสียง
/// วันไหนทีมเปลี่ยนระบบ event ก็แก้แค่ไฟล์นี้ ระบบเสียงไม่ต้องแตะเลย
///
/// เขียนตามแบบ TestEventChannelListener ของ Lead ทุกประการ
/// </summary>
public class GhostVoiceChannelListener : MonoBehaviour
{
    [System.Serializable]
    public struct ChannelBinding
    {
        [Tooltip("Event Channel ที่จะฟัง เช่น TaniAlertedVoidEventChannel")]
        public VoidEventChannelSO channel;

        [Tooltip("เล่นเสียงเตือน (ช่อง Alert Sound) — ดังแน่นอน 100% ไม่มีเงื่อนไข" +
            "\nใช้กับสัญญาณที่ผู้เล่นห้ามพลาด เช่นตอนผีเปลี่ยนสถานะ")]
        public bool playAlertSound;

        [Tooltip("พูดประโยคด้วย — ผ่านระบบสุ่มและ cooldown อาจไม่ดังทุกครั้ง")]
        public bool playVoiceLine;

        [Tooltip("ประโยคของสถานะไหน (ใช้เมื่อติ๊ก Play Voice Line)")]
        public GhostVoiceState voiceState;
    }

    [Header("Dependencies")]
    [Tooltip("เว้นว่างได้ จะหาจาก GameObject ตัวเองอัตโนมัติ")]
    [SerializeField] private GhostSoundBase _soundController;

    [Header("Channel Bindings")]
    [Tooltip("จับคู่ Event Channel กับประโยคที่จะพูด" +
        "\nตอนนี้มี TaniAlertedVoidEventChannel อยู่แล้ว จับคู่กับ Search ได้เลย" +
        "\nส่วน Chase / Attack รอเจ้าของ AI สร้าง channel เพิ่ม")]
    [SerializeField] private ChannelBinding[] _bindings;

    [Header("Debug")]
    [SerializeField] private bool _logEvents = false;

    // เก็บ handler ที่สร้างไว้ เพื่อให้ถอดออกได้ตัวเดียวกันเป๊ะตอน OnDisable
    // ถ้าสร้าง lambda ใหม่ตอนถอด มันจะเป็นคนละ delegate แล้วถอดไม่ออก = memory leak
    private System.Action[] _handlers;

    private void Awake()
    {
        if (_soundController == null)
            _soundController = GetComponent<GhostSoundBase>();

        if (_soundController == null)
        {
            Debug.LogWarning(
                "GhostVoiceChannelListener has no Ghost Sound Controller.",
                this);
        }
    }

    private void OnEnable()
    {
        if (_bindings == null) return;

        _handlers = new System.Action[_bindings.Length];

        for (int i = 0; i < _bindings.Length; i++)
        {
            VoidEventChannelSO channel = _bindings[i].channel;

            if (channel == null)
            {
                Debug.LogWarning(
                    $"GhostVoiceChannelListener binding {i} has no Event Channel.",
                    this);

                continue;
            }

            ChannelBinding binding = _bindings[i];
            _handlers[i] = () => HandleEvent(binding, channel.name);

            channel.Raised += _handlers[i];
        }
    }

    private void OnDisable()
    {
        if (_bindings == null || _handlers == null) return;

        for (int i = 0; i < _bindings.Length && i < _handlers.Length; i++)
        {
            // เช็ค null ทั้งสองฝั่ง — ตอนเปลี่ยนฉากหรือปิดเกม
            // Unity ทำลาย object ไม่เรียงลำดับ ScriptableObject อาจถูกเก็บไปก่อนแล้ว
            if (_bindings[i].channel == null || _handlers[i] == null) continue;

            _bindings[i].channel.Raised -= _handlers[i];
        }

        _handlers = null;
    }

    private void HandleEvent(ChannelBinding binding, string channelName)
    {
        if (_logEvents)
            Debug.Log($"[GhostVoice] {name} รับสัญญาณจาก {channelName}", this);

        if (_soundController == null) return;

        // เสียงเตือนก่อนเสมอ — เป็นข้อมูลที่ผู้เล่นห้ามพลาด
        if (binding.playAlertSound)
            _soundController.PlayAlert();

        // ประโยคพูดเป็นของเสริม ผ่านระบบสุ่มตามปกติ อาจไม่ดังก็ได้
        if (binding.playVoiceLine)
            _soundController.PlayVoiceLine(binding.voiceState);
    }
}