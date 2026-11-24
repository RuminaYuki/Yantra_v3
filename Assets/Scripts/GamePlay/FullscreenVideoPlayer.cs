using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Plays a video on top of everything else (screen space overlay).
/// Put it on any GameObject, then start a video from code, a UnityEvent, or a coroutine:
///
///   player.Play(clip);                                  // fire and forget
///   player.Play(clip, skipped => Debug.Log(skipped));   // with a callback
///   yield return player.PlayRoutine(clip);              // wait inside a coroutine
///   player.Play("https://.../video.mp4");               // from a URL
///
/// The canvas, black background, VideoPlayer and AudioSource are all built at runtime,
/// so nothing else needs to be set up in the scene.
/// </summary>
public class FullscreenVideoPlayer : MonoBehaviour
{
    public enum ScaleType
    {
        /// <summary>Whole video is visible, black bars if the aspect ratio does not match.</summary>
        Fit,
        /// <summary>Video covers the whole screen, edges are cropped if the aspect ratio does not match.</summary>
        Fill
    }

    [Header("Default video (optional)")]
    [Tooltip("Used by the parameterless Play() so a UnityEvent can start it without code.")]
    [SerializeField] private VideoClip _defaultClip;

    [Tooltip("Play the default clip automatically in Start().")]
    [SerializeField] private bool _playOnStart;

    [Header("Display")]
    [SerializeField] private ScaleType _scaleType = ScaleType.Fit;

    [Tooltip("Seconds to fade from black to the video. 0 = instant.")]
    [SerializeField, Min(0f)] private float _fadeInTime = 0.5f;

    [Tooltip("Seconds to fade from the video to transparent when it ends. 0 = instant.")]
    [SerializeField, Min(0f)] private float _fadeOutTime = 0.5f;

    [Tooltip("Canvas sorting order. Keep it higher than any other overlay canvas.")]
    [SerializeField] private int _sortingOrder = 32001;

    [Tooltip("Keep playing when Time.timeScale is 0 (pause menu, slow motion, etc.).")]
    [SerializeField] private bool _ignoreTimeScale = true;

    [Header("Audio")]
    [Tooltip("Optional mixer group for the video sound (for example SFX or Cutscene).")]
    [SerializeField] private AudioMixerGroup _mixerGroup;

    [SerializeField, Range(0f, 1f)] private float _volume = 1f;

    [Header("Play Once")]
    [Tooltip("After a video has started, every later Play() call is ignored (its callback still fires with skipped = true).\n" +
             "Counts even if the player skipped the video. Resets when the scene reloads or ResetPlayed() is called.")]
    [SerializeField] private bool _playOnce;

    [Header("Skip")]
    [Tooltip("Left click, Space, Enter or Escape skips the video.")]
    [SerializeField] private bool _allowSkip = true;

    [Header("Events")]
    [SerializeField] private UnityEvent _onStarted;
    [SerializeField] private UnityEvent _onFinished;

    /// <summary>Raised when the first frame starts showing.</summary>
    public event Action Started;

    /// <summary>Raised when playback ends. The argument is true if the video was skipped, stopped or failed.</summary>
    public event Action<bool> Finished;

    /// <summary>True from the moment Play() is called until the video has fully ended.</summary>
    public bool IsPlaying => _routine != null;

    /// <summary>True once a video has started on this player. Only blocks new videos when Play Once is on.</summary>
    public bool HasPlayed { get; private set; }

    /// <summary>Lets a Play Once player play again (for example after a new game or a retry).</summary>
    public void ResetPlayed()
    {
        HasPlayed = false;
    }

    private Canvas _canvas;
    private CanvasGroup _group;
    private RawImage _image;
    private AspectRatioFitter _aspect;
    private VideoPlayer _player;
    private AudioSource _audio;
    private RenderTexture _texture;

    private Coroutine _routine;
    private Action<bool> _currentCallback;
    private bool _skipRequested;
    private bool _videoEnded;
    private string _error;

    private float DeltaTime => _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

    // ---------- Public API ----------

    /// <summary>Plays the default clip assigned in the inspector.</summary>
    public void Play()
    {
        Play(_defaultClip);
    }

    /// <summary>Plays a clip. onFinished receives true if the video was skipped, stopped or failed.</summary>
    public void Play(VideoClip clip, Action<bool> onFinished = null)
    {
        StartPlayback(clip, null, onFinished);
    }

    /// <summary>Plays a video from a file path or URL.</summary>
    public void Play(string url, Action<bool> onFinished = null)
    {
        StartPlayback(null, url, onFinished);
    }

    /// <summary>Coroutine version: yield return player.PlayRoutine(clip);</summary>
    public IEnumerator PlayRoutine(VideoClip clip)
    {
        Play(clip);
        while (IsPlaying)
            yield return null;
    }

    /// <summary>Coroutine version for a URL.</summary>
    public IEnumerator PlayRoutine(string url)
    {
        Play(url);
        while (IsPlaying)
            yield return null;
    }

    /// <summary>Ends the video the same way a player skip does (fades out).</summary>
    public void Skip()
    {
        if (IsPlaying)
            _skipRequested = true;
    }

    /// <summary>Stops immediately, with no fade.</summary>
    public void Stop()
    {
        if (_routine == null)
            return;

        StopCoroutine(_routine);
        Finish(true);
    }

    // ---------- Unity ----------

    private void Awake()
    {
        BuildScreen();
    }

    private void Start()
    {
        if (_playOnStart)
            Play();
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        ReleaseTexture();
    }

    // ---------- Playback ----------

    private void StartPlayback(VideoClip clip, string url, Action<bool> onFinished)
    {
        if (clip == null && string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("[FullscreenVideoPlayer] Nothing to play: no clip or url given.", this);
            onFinished?.Invoke(true);
            return;
        }

        // Play Once: already played, so do nothing. Callers waiting on PlayRoutine continue right away.
        if (_playOnce && HasPlayed)
        {
            onFinished?.Invoke(true);
            return;
        }

        if (IsPlaying)
            Stop();

        _currentCallback = onFinished;
        _routine = StartCoroutine(Run(clip, url));
    }

    private IEnumerator Run(VideoClip clip, string url)
    {
        _skipRequested = false;
        _videoEnded = false;
        _error = null;

        if (clip != null)
        {
            _player.source = VideoSource.VideoClip;
            _player.clip = clip;
        }
        else
        {
            _player.source = VideoSource.Url;
            _player.url = url;
        }

        _player.timeUpdateMode = _ignoreTimeScale ? VideoTimeUpdateMode.UnscaledGameTime : VideoTimeUpdateMode.GameTime;
        _audio.volume = _volume;
        _audio.outputAudioMixerGroup = _mixerGroup;

        // Load the video first, so the screen only appears when a frame is ready.
        _player.Prepare();
        while (!_player.isPrepared && _error == null && !_skipRequested)
        {
            PollSkip();
            yield return null;
        }

        if (_error != null)
            Debug.LogError($"[FullscreenVideoPlayer] Could not play video: {_error}", this);

        if (_error != null || _skipRequested)
        {
            Finish(true);
            yield break;
        }

        SetupTexture((int)_player.width, (int)_player.height);

        _group.alpha = 0f;
        _canvas.enabled = true;
        _player.Play();
        HasPlayed = true;

        Started?.Invoke();
        _onStarted.Invoke();

        // Playing (fade in happens while the video is already running).
        float elapsed = 0f;
        while (!_videoEnded && !_skipRequested && _error == null)
        {
            elapsed += DeltaTime;
            float alpha = _fadeInTime > 0f ? Mathf.SmoothStep(0f, 1f, elapsed / _fadeInTime) : 1f;
            SetAlpha(alpha);
            PollSkip();
            yield return null;
        }

        if (_error != null)
            Debug.LogError($"[FullscreenVideoPlayer] Video error: {_error}", this);

        // Fade out (video freezes on its last frame while fading).
        float startAlpha = _group.alpha;
        elapsed = 0f;
        while (elapsed < _fadeOutTime)
        {
            elapsed += DeltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, 0f, elapsed / _fadeOutTime));
            yield return null;
        }

        Finish(_skipRequested || _error != null);
    }

    private void Finish(bool skipped)
    {
        _routine = null;

        _player.Stop();
        _player.targetTexture = null;
        ReleaseTexture();

        _canvas.enabled = false;
        _group.alpha = 0f;
        _audio.volume = _volume;

        // Clear the state before the callbacks, so a callback can safely call Play() again.
        Action<bool> callback = _currentCallback;
        _currentCallback = null;

        Finished?.Invoke(skipped);
        _onFinished.Invoke();
        callback?.Invoke(skipped);
    }

    private void SetAlpha(float alpha)
    {
        _group.alpha = alpha;
        _audio.volume = _volume * alpha;
    }

    private void PollSkip()
    {
        if (_allowSkip && SkipPressed())
            _skipRequested = true;
    }

    private static bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        return (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame
                                     || keyboard.enterKey.wasPressedThisFrame
                                     || keyboard.escapeKey.wasPressedThisFrame));
#else
        return Input.GetMouseButtonDown(0)
            || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    // ---------- Video events ----------

    private void OnVideoEnded(VideoPlayer source)
    {
        _videoEnded = true;
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        _error = message;
    }

    // ---------- Screen / texture ----------

    private void SetupTexture(int width, int height)
    {
        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);

        ReleaseTexture();
        _texture = new RenderTexture(width, height, 0) { name = "FullscreenVideo" };
        _texture.Create();

        _player.targetTexture = _texture;
        _image.texture = _texture;

        _aspect.aspectRatio = width / (float)height;
        _aspect.aspectMode = _scaleType == ScaleType.Fill
            ? AspectRatioFitter.AspectMode.EnvelopeParent
            : AspectRatioFitter.AspectMode.FitInParent;
    }

    private void ReleaseTexture()
    {
        if (_texture == null)
            return;

        _texture.Release();
        Destroy(_texture);
        _texture = null;
    }

    private void BuildScreen()
    {
        var root = new GameObject("FullscreenVideo_Canvas", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = _sortingOrder;
        _canvas.enabled = false;

        // Blocks clicks on the UI below while the video is showing.
        root.AddComponent<GraphicRaycaster>();

        _group = root.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        var backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = Color.black;

        var video = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        video.transform.SetParent(root.transform, false);
        var videoRect = (RectTransform)video.transform;
        videoRect.anchorMin = new Vector2(0.5f, 0.5f);
        videoRect.anchorMax = new Vector2(0.5f, 0.5f);
        videoRect.anchoredPosition = Vector2.zero;
        _image = video.GetComponent<RawImage>();
        _image.raycastTarget = false;
        _aspect = video.GetComponent<AspectRatioFitter>();

        _audio = root.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.volume = _volume;
        _audio.outputAudioMixerGroup = _mixerGroup;

        _player = root.AddComponent<VideoPlayer>();
        _player.playOnAwake = false;
        _player.isLooping = false;
        _player.skipOnDrop = true;
        _player.renderMode = VideoRenderMode.RenderTexture;
        _player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        _player.controlledAudioTrackCount = 1;
        _player.EnableAudioTrack(0, true);
        _player.SetTargetAudioSource(0, _audio);
        _player.loopPointReached += OnVideoEnded;
        _player.errorReceived += OnVideoError;
    }
}