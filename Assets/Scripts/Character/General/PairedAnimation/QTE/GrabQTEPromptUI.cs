using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI "กด E รัวๆ" — วางบน Panel ใน Canvas ของ HUD ไม่ต้องผูกกับผีตัวไหน โผล่เองเมื่อมีผีจับ
public class GrabQTEPromptUI : MonoBehaviour
{
    [Header("ชิ้น UI")]
    [Tooltip("CanvasGroup ของทั้งก้อน — ใช้ซ่อน / โชว์")]
    [SerializeField] private CanvasGroup _group;

    [Tooltip("Image แบบ Image Type = Filled")]
    [SerializeField] private Image _fill;

    [Tooltip("ไอคอนปุ่ม E — เด้งทุกครั้งที่กด")]
    [SerializeField] private RectTransform _key;

    [Tooltip("ข้อความเวลาที่เหลือ — ว่างได้")]
    [SerializeField] private TMP_Text _timer;

    [Header("หน้าตา")]
    [SerializeField] private Color _lowColor = new Color(0.85f, 0.15f, 0.1f);
    [SerializeField] private Color _highColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField, Min(0.1f)] private float _fadeSpeed = 8f;
    [SerializeField, Min(0.1f)] private float _fillSpeed = 18f;
    [SerializeField, Min(1f)] private float _punchScale = 1.3f;
    [SerializeField, Min(0.1f)] private float _punchReturn = 14f;

    private GrabQTEInteractAddition _qte;
    private float _shownFill;
    private float _keyScale = 1f;

    private void Awake()
    {
        if (_group == null)
            _group = GetComponent<CanvasGroup>();
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        GrabQTEInteractAddition.AnyStarted += HandleStarted;
        GrabQTEInteractAddition.AnyEnded += HandleEnded;
    }

    private void OnDisable()
    {
        GrabQTEInteractAddition.AnyStarted -= HandleStarted;
        GrabQTEInteractAddition.AnyEnded -= HandleEnded;
        Unbind();
        SetAlpha(0f);
    }

    private void HandleStarted(GrabQTEInteractAddition qte)
    {
        Unbind();
        _qte = qte;
        _qte.Mashed += HandleMashed;
        _shownFill = 0f;
    }

    private void HandleEnded(GrabQTEInteractAddition qte)
    {
        if (qte == _qte)
            Unbind();
    }

    private void Unbind()
    {
        if (_qte != null)
            _qte.Mashed -= HandleMashed;
        _qte = null;
    }

    private void HandleMashed() => _keyScale = _punchScale;

    // ใช้เวลาจริง UI จะได้ขยับต่อแม้เกมหยุดเวลาสั้นๆ (hitstop)
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool show = _qte != null && _qte.IsMashing;

        if (_group != null)
            SetAlpha(Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, _fadeSpeed * dt));

        if (_qte == null)
            return;

        _shownFill = Mathf.Lerp(_shownFill, _qte.Progress01, 1f - Mathf.Exp(-_fillSpeed * dt));
        if (_fill != null)
        {
            _fill.fillAmount = _shownFill;
            _fill.color = Color.Lerp(_lowColor, _highColor, _shownFill);
        }

        _keyScale = Mathf.Lerp(_keyScale, 1f, 1f - Mathf.Exp(-_punchReturn * dt));
        if (_key != null)
            _key.localScale = Vector3.one * _keyScale;

        if (_timer != null)
        {
            float left = _qte.TimeLeft;
            _timer.text = left >= 0f ? left.ToString("0.0") : string.Empty;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (_group == null)
            return;
        _group.alpha = alpha;
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }
}
