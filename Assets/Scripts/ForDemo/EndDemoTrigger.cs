using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// วางบน GameObject เปล่า ระบบจะใส่ Box Collider (Trigger) ให้เอง
// ผู้เล่นเดินชน → จอค่อยๆ ดำ → ขึ้นข้อความทีละหน้า → กลับเมนูหลัก
[RequireComponent(typeof(BoxCollider))]
public class EndDemoTrigger : MonoBehaviour
{
    [Serializable]
    private class Page
    {
        public string Title;
        public string Subtitle;
        [Min(0f)] public float HoldTime = 3f;
    }

    [Header("ผู้เล่น")]
    [SerializeField] private string _playerTag = "Player";

    [Header("ข้อความ (ขึ้นทีละหน้า ตามลำดับ)")]
    [SerializeField]
    private Page[] _pages =
    {
        new Page { Title = "To Be Continued...", Subtitle = "", HoldTime = 2.5f },
        new Page { Title = "Thank You for Playing", Subtitle = "END OF DEMO", HoldTime = 3.5f },
    };

    [Tooltip("เว้นว่าง = ใช้ฟอนต์เริ่มต้นของ TextMeshPro")]
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private float _titleSize = 64f;
    [SerializeField] private float _subtitleSize = 26f;
    [SerializeField] private Color _titleColor = new Color(0.93f, 0.89f, 0.8f);
    [SerializeField] private Color _subtitleColor = new Color(0.85f, 0.68f, 0.4f);

    [Header("เวลา (วินาที)")]
    [SerializeField, Min(0f)] private float _fadeToBlack = 1.5f;
    [SerializeField, Min(0f)] private float _blackBeforeText = 0.6f;
    [SerializeField, Min(0f)] private float _textFade = 1f;
    [SerializeField, Min(0f)] private float _gapBetweenPages = 0.4f;

    [Header("เสียง")]
    [Tooltip("ค่อยๆ ลดเสียงทั้งเกมลงพร้อมจอดำ แล้วคืนเสียงตอนถึงเมนู")]
    [SerializeField] private bool _fadeOutAudio = true;

    [Header("กลับเมนู")]
    [Tooltip("ใช้เฉพาะตอนไม่มี SceneLoader ในฉาก (เช่นกด Play ที่ฉากนี้ตรงๆ) — ใส่ชื่อฉากเมนูหลัก")]
    [SerializeField] private string _fallbackMenuScene = "";

    [Tooltip("ต้องยาวกว่า Fade To Black Duration ของ SceneLoader นิดหน่อย จอจะได้ไม่กระพริบตอนส่งต่อ")]
    [SerializeField, Min(0f)] private float _handoffDelay = 0.6f;

    private bool _triggered;
    private GameObject _overlay;
    private CanvasGroup _black;
    private CanvasGroup _text;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _subtitle;
    private PauseController _disabledPause;
    private float _startVolume = 1f;
    private bool _audioMuted;

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4f, 3f, 1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered || !IsPlayer(other))
            return;

        _triggered = true;
        StartCoroutine(EndRoutine());
    }

    // collider ที่ชนจริงอาจอยู่ที่ตัวลูกของผู้เล่น เลยไล่หา Tag ขึ้นไปทางตัวแม่
    private bool IsPlayer(Collider other)
    {
        for (Transform t = other.transform; t != null; t = t.parent)
            if (t.CompareTag(_playerTag))
                return true;
        return false;
    }

    private IEnumerator EndRoutine()
    {
        // หยุดผู้เล่นกับผี ไม่ให้โดนตีตอนจอดำ และปิดปุ่ม ESC ไม่ให้เปิดหน้า pause ซ้อน
        StateMachineController.IsPaused = true;
        if (PauseController.Instance != null && PauseController.Instance.enabled)
        {
            _disabledPause = PauseController.Instance;
            _disabledPause.enabled = false;
        }

        BuildOverlay();

        _startVolume = AudioListener.volume;
        _audioMuted = _fadeOutAudio;
        yield return Fade(_fadeToBlack, t =>
        {
            _black.alpha = t;
            if (_fadeOutAudio)
                AudioListener.volume = Mathf.Lerp(_startVolume, 0f, t);
        });

        yield return new WaitForSecondsRealtime(_blackBeforeText);

        foreach (Page page in _pages)
        {
            _title.text = page.Title;
            _subtitle.text = page.Subtitle;
            yield return Fade(_textFade, t => _text.alpha = t);
            yield return new WaitForSecondsRealtime(page.HoldTime);
            yield return Fade(_textFade, t => _text.alpha = 1f - t);
            yield return new WaitForSecondsRealtime(_gapBetweenPages);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadMainMenu();
            // รอให้จอดำของ SceneLoader ขึ้นเต็มก่อน แล้วค่อยเอาจอดำของเราออก จะได้เห็นหน้าโหลดตามปกติ
            yield return new WaitForSecondsRealtime(_handoffDelay);
            if (_overlay != null)
                Destroy(_overlay);
        }
        else if (!string.IsNullOrEmpty(_fallbackMenuScene))
        {
            RestoreState();
            StateMachineController.IsPaused = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(_fallbackMenuScene);
        }
        else
        {
            Debug.LogError("[EndDemo] ไม่เจอ SceneLoader และไม่ได้ใส่ชื่อฉากเมนูในช่อง Fallback Menu Scene", this);
        }
    }

    private static IEnumerator Fade(float duration, Action<float> apply)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            apply(Mathf.Clamp01(time / duration));
            yield return null;
        }
        apply(1f);
    }

    // ตอนฉากถูกปิด (สลับไปเมนู) เสียงกับปุ่ม pause จะกลับมาเป็นเหมือนเดิม
    private void OnDestroy() => RestoreState();

    private void RestoreState()
    {
        if (_audioMuted)
        {
            AudioListener.volume = _startVolume;
            _audioMuted = false;
        }
        if (_disabledPause != null)
        {
            _disabledPause.enabled = true;
            _disabledPause = null;
        }
    }

    private void BuildOverlay()
    {
        _overlay = new GameObject("EndDemoOverlay");
        var canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = _overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform black = CreateRect("Black", _overlay.transform);
        var image = black.gameObject.AddComponent<Image>();
        image.color = Color.black;
        _black = black.gameObject.AddComponent<CanvasGroup>();
        _black.alpha = 0f;

        RectTransform textRoot = CreateRect("Text", _overlay.transform);
        _text = textRoot.gameObject.AddComponent<CanvasGroup>();
        _text.alpha = 0f;
        _text.blocksRaycasts = false;

        _title = CreateText("Title", textRoot, _titleSize, _titleColor, 0f, new Vector2(0f, 25f));
        _subtitle = CreateText("Subtitle", textRoot, _subtitleSize, _subtitleColor, 18f, new Vector2(0f, -55f));
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, float size, Color color, float spacing, Vector2 offset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1600f, 120f);
        rect.anchoredPosition = offset;

        var text = go.AddComponent<TextMeshProUGUI>();
        if (_font != null)
            text.font = _font;
        text.fontSize = size;
        text.color = color;
        text.characterSpacing = spacing;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null)
            return;
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}