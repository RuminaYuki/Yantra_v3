using UnityEngine;

public class DissolveSoundPlayer : MonoBehaviour
{
    [Tooltip("เสียงตอนผีสลายหาย")]
    [SerializeField] private SoundID _sound;

    // ต่อเข้าช่อง On Dissolve Started ของ DissolveVFXPlayer
    public void Play()
    {
        // ฉากที่ไม่มี SoundManager (ฉากเทสของเพื่อน) จะเงียบเฉยๆ ไม่พัง
        if (_sound == null || SoundManager.Instance == null)
            return;

        SoundManager.Instance.PlaySFX(_sound, transform.position);
    }
}
