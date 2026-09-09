using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System;

[Serializable]
public class SoundData
{
#if UNITY_EDITOR
    [SerializeField, HideInInspector]
    private string idName;

    public void UpdateName()
    {
        idName = id != null ? id.name : "None";
    }
#endif

    [Header("Identity & Routing")]
    public SoundID id;
    public AudioMixerGroup mixerGroup;

    [Header("Voice Priority")]
    [Tooltip("0 = สำคัญที่สุด ห้ามโดนตัด / 255 = ตัดทิ้งได้ก่อนเพื่อน" +
        "\nUnity เล่นได้จริงแค่ 32 เสียงพร้อมกัน เกินนั้นจะเลือกตัดเอง" +
        "\nถ้าไม่ตั้ง มันจะตัดตัวที่เบาที่สุด ซึ่งมักเป็น ambient ที่ขาดไม่ได้" +
        "\nแนะนำ: BGM 0 / เสียงพูด 10 / ambient 32 / เสียงร้องผี 60 / ฝีเท้าผู้เล่น 100 / ฝีเท้าผี 180 / foley 200")]
    [Range(0, 255)] public int priority = 128;

    [Header("Audio Clip")]
    [Tooltip("เปิดเพื่อใช้ระบบสุ่มเสียงจากหลายๆ ไฟล์ (ถ้าปิด จะใช้แค่ช่อง Clip เดี่ยวๆ)")]
    public bool useRandomClips = false;

    [Header("Random Chance")]
    [Tooltip("โอกาสที่จะหยิบเสียงจากใน List มาเล่น (100 = สุ่มจาก List เสมอ, 20 = มีโอกาสแค่ 20%)")]
    [Range(0f, 100f)] public float randomChance = 100f;

    [Tooltip("ใส่ไฟล์เสียงที่นี่ (กรณีปิดการสุ่ม)")]
    public AudioClip clip;

    [Tooltip("ใส่ไฟล์เสียงหลายๆ ไฟล์ที่นี่ (กรณีเปิดการสุ่ม)")]
    public AudioClip[] clips;

    [Header("Random Start Offset")]
    [Tooltip("สุ่มจุดเริ่มเล่นในไฟล์ ทำให้ไฟล์เดียวให้ 'รูปทรงความดัง' คนละแบบทุกครั้ง\n" +
        "เหมาะกับเสียงต่อเนื่องอย่างผ้าเสียดสี / ห้ามใช้กับเสียงที่มีหัวชัดเจนอย่างฝีเท้า เพราะจะแหว่ง")]
    public bool useRandomStartOffset = false;

    [Tooltip("เริ่มเล่นได้ลึกสุดกี่ % ของไฟล์ / 0.6 = สุ่มเริ่มที่ไหนก็ได้ในช่วง 60% แรก\n" +
        "อย่าตั้งสูงเกินไป ไม่งั้นจะไปเริ่มตรงหางเสียงที่เบาจนแทบไม่ได้ยิน")]
    [Range(0f, 0.95f)] public float maxStartOffsetPercent = 0.5f;

    [Header("Pitch Settings")]
    [Tooltip("เปิดเพื่อสุ่มเสียงแหลม/ทุ้ม (ทำให้เสียงดูไม่ซ้ำซาก)")]
    public bool useRandomPitch = false;
    [Range(0.1f, 3f)] public float minPitch = 0.9f;
    [Range(0.1f, 3f)] public float maxPitch = 1.1f;

    [Header("Volume & 3D Settings")]
    [Tooltip("เปิดเพื่อสุ่มความดังแต่ละก้าว (น้ำหนักเท้าหนัก/เบา)")]
    public bool useRandomVolume = false;
    [Range(0f, 1f)] public float minVolume = 0.85f;
    [Range(0f, 1f)] public float maxVolume = 1.0f;

    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("0 = 2D (เสียงดังเท่ากันหมด), 1 = 3D (ดังตามระยะทาง)")]
    [Range(0f, 1f)] public float spatialBlend = 1f;

    [Header("3D Distance Settings")]
    [Tooltip("ระยะใกล้สุดที่จะได้ยินเสียงดังเต็ม 100%")]
    public float minDistance = 1f;
    [Tooltip("ระยะไกลสุดที่เสียงจะเบาลงจน 'ดับสนิท' (แนะนำ: ฝีเท้าผี = 10, เสียงร้อง = 20)")]
    public float maxDistance = 15f;

    // ==========================================
    // Core Logic
    // ==========================================

    public AudioClip GetClip()
    {
        if (!useRandomClips || clips == null || clips.Length == 0)
            return clip;

        if (UnityEngine.Random.Range(0f, 100f) <= randomChance)
        {
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        return clip;
    }

    public float GetStartOffset(AudioClip targetClip)
    {
        if (!useRandomStartOffset || targetClip == null) return 0f;
        if (targetClip.length < 0.1f) return 0f;   // ไฟล์สั้นมาก ตัดหัวแล้วจะไม่เหลืออะไร

        float maxOffset = targetClip.length * Mathf.Clamp01(maxStartOffsetPercent);
        return UnityEngine.Random.Range(0f, maxOffset);
    }

    public float GetPitch()
    {
        if (useRandomPitch)
            return UnityEngine.Random.Range(minPitch, maxPitch);

        return 1f;
    }

    public float GetVolume()
    {
        if (useRandomVolume)
            return UnityEngine.Random.Range(minVolume, maxVolume);

        return volume;
    }
}

[CreateAssetMenu(menuName = "Sound/SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    public List<SoundData> sounds;

#if UNITY_EDITOR
    private void OnValidate()
    {
        foreach (var s in sounds)
        {
            if (s != null)
                s.UpdateName();
        }
    }
#endif
}