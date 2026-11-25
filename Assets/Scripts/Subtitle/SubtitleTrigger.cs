using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SubtitleTrigger : MonoBehaviour
{
    [Header("Subtitle Settings")]
    [Tooltip("ลากไฟล์บทพูด (SubtitleDataSO) ที่อยากให้เล่นตอนเดินผ่านมาใส่ช่องนี้")]
    [SerializeField] private SubtitleDataSO _subtitleData;

    [Tooltip("ถ้าติ๊กถูก ระบบจะเล่นแค่ครั้งเดียวแล้วปิดตัวเองทิ้ง (กันผู้เล่นเดินถอยหลังกลับมาเหยียบซ้ำ)")]
    [SerializeField] private bool _playOnce = true;

    [Tooltip("tag ของผู้เล่น — เช็คไล่ขึ้นไปทางตัวแม่ด้วย เพราะใน FPP collider ที่ชนจริงอยู่ที่ตัวลูก")]
    [SerializeField] private string _playerTag = "Player";

    [Tooltip("เล่นจบแล้วต้องรอกี่วินาทีถึงจะ trigger ใหม่ได้ (ใช้ตอนไม่ติ๊ก Play Once)")]
    [SerializeField, Min(0f)] private float _retriggerDelay = 1f;

    private bool _isPlayingMine;
    private float _readyTime;

    private void Update()
    {
        if (!_isPlayingMine)
            return;

        SubtitleSystem system = SubtitleSystem.Instance;
        if (system != null && system.IsPlaying)
            return;

        // เล่นจบแล้ว (หรือถูกหยุด เช่น ผู้เล่นตาย) → เริ่มนับ delay
        _isPlayingMine = false;
        _readyTime = Time.time + _retriggerDelay;
    }

    // ทำงานตอน Add Component — ตั้ง Is Trigger ให้เลย ไม่ต้องจำไปติ๊กเอง
    private void Reset()
    {
        if (TryGetComponent(out Collider col))
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        // ยังพูดไม่จบ หรือยังไม่พ้น delay — ไม่เริ่มใหม่ตั้งแต่ต้น
        if (_isPlayingMine || Time.time < _readyTime)
            return;

        if (_subtitleData == null)
        {
            Debug.LogWarning($"[SubtitleTrigger] {name} ยังไม่ได้ใส่ไฟล์บทพูด", this);
            return;
        }

        if (SubtitleSystem.Instance == null)
        {
            Debug.LogWarning("[SubtitleTrigger] ไม่มี SubtitleSystem ใน scene นี้", this);
            return;
        }

        SubtitleSystem.Instance.PlaySubtitle(_subtitleData);
        _isPlayingMine = true;

        // ปิดหลังเล่นจริงเท่านั้น — ถ้าเล่นไม่ได้แล้วปิดทิ้ง บทนี้จะหายไปเลยจนกว่าจะโหลดด่านใหม่
        if (_playOnce)
            gameObject.SetActive(false);
    }

    private bool IsPlayer(Collider other)
    {
        for (Transform t = other.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(_playerTag))
                return true;
        }

        return false;
    }
}