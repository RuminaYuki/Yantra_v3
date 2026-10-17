using SDFcl.GamePlay.Interactable;
using UnityEngine;

[RequireComponent(typeof(Iinteractor), typeof(InteractorEvent))]
public class SuptitleAddition : MonoBehaviour
{
    [SerializeField] SubtitleDataSO subtitleData;

    public void PlaySubtitle()
    {
        if (subtitleData == null)
        {
            Debug.LogWarning($"[SuptitleAddition] {name} ยังไม่ได้ใส่ไฟล์บทพูด", this);
            return;
        }
        if (SubtitleSystem.Instance == null)
        {
            Debug.LogWarning("[SuptitleAddition] ไม่มี SubtitleSystem ใน scene นี้", this);
            return;
        }
        SubtitleSystem.Instance.PlaySubtitle(subtitleData);
    }
}
