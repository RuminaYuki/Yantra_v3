using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yantra.UI;

// วางบน GameObject เปล่าหน้าบาเรีย ระบบจะใส่ Box Collider (Trigger) ให้เอง
// ผู้เล่นเดินเข้ามาตอนบาเรียยังไม่ปลด → ขึ้นข้อความเตือน
[RequireComponent(typeof(BoxCollider))]
public class BarrierLockedHint : MonoBehaviour
{
    [Header("บาเรีย")]
    [Tooltip("Collider ที่กั้นทางของบาเรีย — ตราบใดที่ยังกั้นอยู่ (ไม่ใช่ Trigger) ถือว่ายังไม่ปลด\n" +
             "ตอนปลด ระบบเดิมตั้ง Is Trigger = true อยู่แล้ว ข้อความจะเลิกขึ้นเอง")]
    [SerializeField] private Collider _barrierCollider;

    [SerializeField] private string _playerTag = "Player";

    [Header("ข้อความ")]
    [SerializeField, TextArea] private string _message = "Defeat all the spirits to break this barrier";
    [Tooltip("เว้นว่าง = ฟอนต์เริ่มต้นของ TextMeshPro (ถ้าจะใช้ภาษาไทย ต้องใส่ฟอนต์ที่มีภาษาไทย)")]
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private float _fontSize = 34f;
    [SerializeField] private Color _color = new Color(0.95f, 0.85f, 0.6f);
    [Tooltip("ความสูงจากกลางจอ — ติดลบ = ต่ำกว่ากลางจอ")]
    [SerializeField] private float _heightFromCenter = -220f;

    [Header("เวลา (วินาที)")]
    [SerializeField, Min(0f)] private float _showTime = 3f;
    [SerializeField, Min(0.01f)] private float _fadeTime = 0.4f;
    [Tooltip("ยืนแช่อยู่ในเขต ข้อความจะขึ้นซ้ำหลังจากหายไปกี่วินาที")]
    [SerializeField, Min(0f)] private float _repeatDelay = 4f;

    private CanvasGroup _group;
    private float _alpha;
    private float _hideAt;
    private float _canShowAt;
    private bool _playerInside;
    private bool _unlocked;

    public bool IsLocked => !_unlocked && _barrierCollider != null && _barrierCollider.enabled
                            && _barrierCollider.gameObject.activeInHierarchy && !_barrierCollider.isTrigger;

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4f, 3f, 2f);
    }

    private void Awake()
    {
        if (_barrierCollider == null)
            Debug.LogWarning("[BarrierHint] ยังไม่ได้ใส่ Barrier Collider — ข้อความจะไม่ขึ้น", this);
    }

    /// <summary>ต่อจาก UnityEvent ได้ ถ้าบาเรียถูกปลดด้วยวิธีอื่นที่ไม่ได้แตะ Collider</summary>
    public void Unlock() => _unlocked = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;
        _playerInside = true;
        _canShowAt = 0f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            _playerInside = false;
    }

    private void OnDisable() => _playerInside = false;

    private void Update()
    {
        if (_playerInside && IsLocked && Time.time >= _canShowAt && !MenuOpen())
        {
            _hideAt = Time.time + _showTime;
            _canShowAt = _hideAt + _repeatDelay;
            EnsureOverlay();
        }

        bool visible = IsLocked && Time.time < _hideAt && !MenuOpen();
        float target = visible ? 1f : 0f;
        if (_group == null || Mathf.Approximately(_alpha, target))
            return;

        _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime / _fadeTime);
        _group.alpha = _alpha;
    }

    private static bool MenuOpen()
    {
        UIManager ui = UIManager.Instance;
        return ui != null && ui.HasAnyOpen;
    }

    // collider ที่ชนจริงอาจอยู่ที่ตัวลูกของผู้เล่น เลยไล่หา Tag ขึ้นไปทางตัวแม่
    private bool IsPlayer(Collider other)
    {
        for (Transform t = other.transform; t != null; t = t.parent)
            if (t.CompareTag(_playerTag))
                return true;
        return false;
    }

    private void EnsureOverlay()
    {
        if (_group != null)
            return;

        var root = new GameObject("BarrierHintOverlay");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _group = root.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _alpha = 0f;

        var back = new GameObject("Back", typeof(RectTransform));
        var backRect = (RectTransform)back.transform;
        backRect.SetParent(root.transform, false);
        backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.sizeDelta = new Vector2(1200f, _fontSize * 2.2f);
        backRect.anchoredPosition = new Vector2(0f, _heightFromCenter);
        var image = back.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);
        image.raycastTarget = false;

        var label = new GameObject("Message", typeof(RectTransform));
        var labelRect = (RectTransform)label.transform;
        labelRect.SetParent(backRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(30f, 0f);
        labelRect.offsetMax = new Vector2(-30f, 0f);
        var text = label.AddComponent<TextMeshProUGUI>();
        if (_font != null)
            text.font = _font;
        text.text = _message;
        text.fontSize = _fontSize;
        text.color = _color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }
}
