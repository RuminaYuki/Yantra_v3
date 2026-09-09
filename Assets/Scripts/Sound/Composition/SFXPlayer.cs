using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SFXPlayer : MonoBehaviour
{
    private AudioSource audioSource;
    private Coroutine returnRoutine;
    private Coroutine fadeRoutine;
    private float baseVolume = 1f;

    [HideInInspector] public string myPoolTag;

    private Transform targetToFollow;

    private int version = 0;
    public int Version => version;

    public bool IsPlaying => audioSource != null && audioSource.isPlaying;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void LateUpdate()
    {
        if (targetToFollow == null) return;
        transform.position = targetToFollow.position;
    }

    public void FollowTarget(Transform target)
    {
        targetToFollow = target;
    }

    private float Setup(SoundData data, bool loop)
    {
        AudioClip clipToPlay = data.GetClip();

        if (clipToPlay == null)
        {
            Debug.LogWarning("[SFXPlayer] SoundData missing AudioClip");
            ReturnHome();
            return 0f;
        }

        // [NEW] เริ่มงานใหม่ = ขึ้นรุ่นใหม่ ใบเสร็จเก่าทุกใบหมดอายุทันที
        version++;

        if (returnRoutine != null) { StopCoroutine(returnRoutine); returnRoutine = null; }
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }

        float pitch = data.GetPitch();
        baseVolume = data.GetVolume();

        audioSource.clip = clipToPlay;
        audioSource.volume = baseVolume;
        audioSource.pitch = pitch;

        audioSource.spatialBlend = data.spatialBlend;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = data.minDistance;
        audioSource.maxDistance = data.maxDistance;

        audioSource.loop = loop;

        // ลำโพงมาจากพูล ถ้าไม่เซ็ตทุกครั้ง มันจะพก priority ของงานก่อนหน้าติดมาด้วย
        audioSource.priority = Mathf.Clamp(data.priority, 0, 255);

        if (data.mixerGroup != null)
            audioSource.outputAudioMixerGroup = data.mixerGroup;

        // ไม่ทำกับเสียงลูป เพราะลูปเล่นยาวอยู่แล้ว จุดเริ่มไม่มีความหมาย
        float startOffset = loop ? 0f : data.GetStartOffset(clipToPlay);
        audioSource.time = startOffset;   // ต้องเซ็ตหลังใส่ clip และก่อน Play()

        audioSource.Play();

        // หักเวลาที่ข้ามไปออกด้วย ไม่งั้นจะคำนวณเวลาคืนลำโพงนานเกินจริง
        float remaining = clipToPlay.length - startOffset;
        return remaining / Mathf.Max(0.01f, Mathf.Abs(pitch));
    }

    public float Play(SoundData data)
    {
        float duration = Setup(data, false);
        if (duration <= 0f) return 0f;
        returnRoutine = StartCoroutine(ReturnAfterFinished(duration));
        return duration;
    }

    public float PlayLoop(SoundData data, float duration)
    {
        float clipLength = Setup(data, true);
        if (clipLength <= 0f) return 0f;
        returnRoutine = StartCoroutine(ReturnAfterFinished(duration));
        return duration;
    }

    public void PlayLoopForever(SoundData data)
    {
        Setup(data, true);
    }

    public void Stop()
    {
        if (returnRoutine != null) { StopCoroutine(returnRoutine); returnRoutine = null; }
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
        if (audioSource != null) audioSource.Stop();
        ReturnHome();
    }

    public void FadeOutAndStop(float fadeTime)
    {
        if (returnRoutine != null) { StopCoroutine(returnRoutine); returnRoutine = null; }
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
        fadeRoutine = StartCoroutine(FadeOutStopRoutine(fadeTime));
    }

    private IEnumerator FadeOutStopRoutine(float fadeTime)
    {
        float startVolume = audioSource.volume;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
            yield return null;
        }
        audioSource.Stop();
        audioSource.volume = baseVolume;
        fadeRoutine = null;
        ReturnHome();
    }

    public void SetVolumeMultiplier(float multiplier)
    {
        if (audioSource != null)
            audioSource.volume = baseVolume * Mathf.Clamp01(multiplier);
    }

    public void FadeToVolumeMultiplier(float targetMultiplier, float fadeTime)
    {
        if (!gameObject.activeInHierarchy) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeVolumeRoutine(targetMultiplier, fadeTime));
    }

    private IEnumerator FadeVolumeRoutine(float targetMultiplier, float fadeTime)
    {
        float startVol = audioSource.volume;
        float targetVol = baseVolume * Mathf.Clamp01(targetMultiplier);
        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVol, targetVol, t / fadeTime);
            yield return null;
        }

        audioSource.volume = targetVol;
        fadeRoutine = null;
    }

    private IEnumerator ReturnAfterFinished(float duration)
    {
        float target = duration + 0.15f;
        float t = 0f;

        while (t < target)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        returnRoutine = null;
        ReturnHome();
    }

    private void ReturnHome()
    {
        targetToFollow = null;

        if (!gameObject.activeInHierarchy) return;
        if (ObjectPooler.Instance != null && !string.IsNullOrEmpty(myPoolTag))
            ObjectPooler.Instance.ReturnToPool(myPoolTag, gameObject);
        else
            Destroy(gameObject);
    }

    private void OnDisable()
    {
        // [NEW] กลับเข้าโกดัง = ใบเสร็จทุกใบที่ออกไปหมดอายุทันที
        // จุดนี้สำคัญ เพราะทุกเส้นทางการคืนของจบที่ SetActive(false) เสมอ
        version++;

        targetToFollow = null;
        if (returnRoutine != null) { StopCoroutine(returnRoutine); returnRoutine = null; }
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
        if (audioSource != null) { audioSource.Stop(); audioSource.clip = null; }
    }
}