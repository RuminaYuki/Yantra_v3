using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แม่แบบของแถบ HUD ทุกอัน
///
/// อ่านค่าทุกเฟรมแทนการใช้ event เพราะ Guard ลด/ฟื้นตัวทุกเฟรม
/// ซึ่ง event ที่มีอยู่ไม่ได้ยิงในจังหวะนั้น ถ้าใช้ event แถบจะกระตุกเป็นช่วง ๆ
///
/// คลาสลูกแค่บอกว่าอ่านค่าจากไหน (ดู HealthBarUI / GuardBarUI)
/// </summary>
public abstract class StatBarUI : MonoBehaviour
{
    [Header("Fill")]
    [Tooltip("Image ที่ตั้ง Image Type = Filled")]
    [SerializeField] protected Image _fill;

    [Tooltip("ความเร็วที่แถบวิ่งตามค่าจริง — 0 = เปลี่ยนทันที")]
    [SerializeField] private float _fillSpeed = 3f;

    [Header("Delayed Fill (ไม่ใส่ก็ได้)")]
    [Tooltip("แถบสีจางที่ไล่ตามช้า ๆ ทำให้เห็นว่าเพิ่งเสียไปเท่าไหร่")]
    [SerializeField] private Image _delayedFill;
    [SerializeField] private float _delayedSpeed = 0.8f;
    [SerializeField] private float _delayedHold = 0.4f;

    [Header("Text (ไม่ใส่ก็ได้)")]
    [SerializeField] private TMP_Text _valueText;

    [Tooltip("รูปแบบข้อความ — {0} คือค่าปัจจุบัน, {1} คือค่าสูงสุด")]
    [SerializeField] private string _format = "{0}";

    [Header("Color")]
    [SerializeField] private Color _normalColor = Color.white;

    [Tooltip("สีตอนเหลือน้อย")]
    [SerializeField] private Color _lowColor = new(0.9f, 0.2f, 0.2f, 1f);

    [Tooltip("ต่ำกว่ากี่ส่วนถึงเปลี่ยนเป็นสีเตือน (0.3 = 30%)")]
    [Range(0f, 1f)][SerializeField] private float _lowThreshold = 0.3f;

    [Header("Auto Hide")]
    [Tooltip("ซ่อนแถบตอนค่าเต็ม — เหมาะกับ Guard ที่ไม่ต้องโชว์ตลอด")]
    [SerializeField] private bool _hideWhenFull = false;
    [SerializeField] private float _hideDelay = 1.5f;
    [SerializeField] private float _fadeSpeed = 4f;

    private CanvasGroup _canvasGroup;
    private float _displayed = 1f;
    private float _delayedDisplayed = 1f;
    private float _delayedTimer;
    private float _fullTimer;

    /// <summary>คลาสลูกบอกว่าอ่านค่าจากไหน — คืน false ถ้ายังหาแหล่งข้อมูลไม่เจอ</summary>
    protected abstract bool TryReadValue(out float current, out float max);

    protected virtual void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null && _hideWhenFull)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    protected virtual void Update()
    {
        if (!TryReadValue(out float current, out float max)) return;
        if (max <= 0f) return;

        float target = Mathf.Clamp01(current / max);

        UpdateFill(target);
        UpdateDelayedFill(target);
        UpdateText(current, max);
        UpdateColor(target);
        UpdateVisibility(target);
    }

    // ---------- ภายใน ----------

    private void UpdateFill(float target)
    {
        if (_fill == null) return;

        _displayed = _fillSpeed <= 0f
            ? target
            : Mathf.MoveTowards(_displayed, target, _fillSpeed * Time.deltaTime);

        _fill.fillAmount = _displayed;
    }

    /// <summary>
    /// แถบจางที่ไล่ตามช้า ๆ — ทำให้คนเล่นเห็นว่า "เพิ่งเสียไปเท่านี้"
    /// เป็นเทคนิคที่เกมต่อสู้ใช้กันทั่วไป ช่วยให้อ่านความเสียหายออกโดยไม่ต้องมีตัวเลขเด้ง
    /// </summary>
    private void UpdateDelayedFill(float target)
    {
        if (_delayedFill == null) return;

        if (_delayedDisplayed > target)
        {
            _delayedTimer += Time.deltaTime;
            if (_delayedTimer >= _delayedHold)
                _delayedDisplayed = Mathf.MoveTowards(_delayedDisplayed, target, _delayedSpeed * Time.deltaTime);
        }
        else
        {
            // ค่าเพิ่มขึ้น (ฟื้นตัว) ให้ตามทันทีไม่ต้องหน่วง
            _delayedDisplayed = target;
            _delayedTimer = 0f;
        }

        _delayedFill.fillAmount = _delayedDisplayed;
    }

    private void UpdateText(float current, float max)
    {
        if (_valueText == null) return;
        _valueText.text = string.Format(_format, Mathf.CeilToInt(current), Mathf.CeilToInt(max));
    }

    private void UpdateColor(float target)
    {
        if (_fill == null) return;
        _fill.color = target <= _lowThreshold ? _lowColor : _normalColor;
    }

    private void UpdateVisibility(float target)
    {
        if (!_hideWhenFull || _canvasGroup == null) return;

        bool isFull = target >= 0.999f;

        if (isFull) _fullTimer += Time.deltaTime;
        else _fullTimer = 0f;

        float targetAlpha = (isFull && _fullTimer >= _hideDelay) ? 0f : 1f;

        _canvasGroup.alpha = Mathf.MoveTowards(
            _canvasGroup.alpha, targetAlpha, _fadeSpeed * Time.deltaTime);
    }
}
