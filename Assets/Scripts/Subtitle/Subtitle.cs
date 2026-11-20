using UnityEngine;

public class Subtitle : MonoBehaviour
{
    [Header("Subtitle Settings")]
    [Tooltip("ลากไฟล์บทพูด (SubtitleDataSO) ที่อยากให้เล่นตอนเดินผ่านมาใส่ช่องนี้")]
    [SerializeField] private SubtitleDataSO _subtitleData;

    [ContextMenu("Play Subtitle")]
    public void Play()
    {
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
    }
}
