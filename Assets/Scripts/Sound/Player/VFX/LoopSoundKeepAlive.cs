using UnityEngine;

public class LoopSoundKeepAlive : MonoBehaviour
{
    private SFXHandle _handle;
    private SoundID _exitSound;
    private Transform _origin;
    private float _graceSeconds;
    private float _fadeOutSeconds;
    private float _lastPing;
    private bool _running;

    /// <summary>เริ่มดูแลเสียงลูปนี้</summary>
    public void Begin(
        SFXHandle handle,
        SoundID exitSound,
        Transform origin,
        float graceSeconds,
        float fadeOutSeconds)
    {
        _handle = handle;
        _exitSound = exitSound;
        _origin = origin;
        _graceSeconds = Mathf.Max(0.02f, graceSeconds);
        _fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
        _running = true;

        Ping();
    }

    /// <summary>ต่ออายุ — เรียกทุกเฟรมที่ยังต้องการให้เสียงดังต่อ</summary>
    public void Ping() => _lastPing = Time.time;

    private void Update()
    {
        if (!_running) return;
        if (Time.time - _lastPing <= _graceSeconds) return;

        Finish();
    }

    /// <summary>
    /// กันเสียงค้างตอนตัวละครถูกปิดหรือถูกทำลายกลางคัน
    /// เสียงลูปไม่หยุดเองถ้าไม่มีใครสั่ง จะดังค้างไปตลอดเกม
    /// </summary>
    private void OnDisable()
    {
        if (!_running) return;

        StopLoop();
        _running = false;
    }

    private void Finish()
    {
        _running = false;

        StopLoop();
        PlayExitSound();

        Destroy(this);
    }

    private void StopLoop()
    {
        if (!_handle.IsValid) return;

        if (_fadeOutSeconds > 0f) _handle.FadeOutAndStop(_fadeOutSeconds);
        else _handle.Stop();

        _handle = SFXHandle.None;
    }

    private void PlayExitSound()
    {
        if (_exitSound == null) return;
        if (SoundManager.Instance == null) return;

        Transform target = _origin != null ? _origin : transform;
        SoundManager.Instance.PlaySFXAttached(_exitSound, target);
    }
}