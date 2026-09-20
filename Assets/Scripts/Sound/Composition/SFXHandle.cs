using UnityEngine;

/// <summary>
/// "ใบเสร็จ" ที่ SoundManager ออกให้ตอนสั่งเล่นเสียงแบบลูป
/// </summary>
public readonly struct SFXHandle
{
    private readonly SFXPlayer player;
    private readonly int version;

    /// <summary>ใบเสร็จเปล่า ใช้แทนค่า null (struct ใส่ null ไม่ได้)</summary>
    public static SFXHandle None => default;

    public SFXHandle(SFXPlayer player, int version)
    {
        this.player = player;
        this.version = version;
    }

    /// <summary>
    /// ใบเสร็จนี้ยังใช้ได้อยู่ไหม
    /// - player != null    : ลำโพงยังไม่ถูก Destroy (Unity override == ให้ตรวจ destroyed object ได้)
    /// - Version ตรงกัน    : ลำโพงยังทำงานเดิมอยู่ ยังไม่ถูกเอาไปใช้เล่นเสียงอื่น
    /// </summary>
    public bool IsValid => player != null && player.Version == version;

    public bool IsPlaying => IsValid && player.IsPlaying;

    // ==========================================
    // คำสั่งทั้งหมดผ่านด่านตรวจ IsValid ก่อนเสมอ
    // ==========================================

    public void SetVolumeMultiplier(float multiplier)
    {
        if (IsValid) player.SetVolumeMultiplier(multiplier);
    }

    public void FadeToVolumeMultiplier(float targetMultiplier, float fadeTime)
    {
        if (IsValid) player.FadeToVolumeMultiplier(targetMultiplier, fadeTime);
    }

    public void FadeOutAndStop(float fadeTime)
    {
        if (IsValid) player.FadeOutAndStop(fadeTime);
    }

    public void Stop()
    {
        if (IsValid) player.Stop();
    }
}