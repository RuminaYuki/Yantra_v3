using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yantra.UI;

// แถบดำบน-ล่าง (letterbox) ตอน Sequence เล่นอยู่ — แบบเดียวกับ GhostEncounterCutscene
// ติดบน GameObject เดียวกับ Sequence ได้เลย ไม่ต้องต่อ event: เช็ค Sequence.IsPlaying ทุกเฟรมเอง
[RequireComponent(typeof(Sequence))]
public class SequenceLetterbox : MonoBehaviour
{
    [Tooltip("ความสูงแถบดำต่อข้าง (สัดส่วนของจอ) — 0.1 = แถบละ 10%")]
    [SerializeField, Range(0f, 0.25f)] private float _size = 0.1f;

    [Tooltip("แถบเลื่อนเข้า / ออกใช้เวลากี่วินาที (เวลาจริง)")]
    [SerializeField, Min(0.01f)] private float _fadeTime = 0.35f;

    // อยู่เหนือ HUD แต่ต่ำกว่าเมนู — เมนูเปิดก็ซ่อนแถบอยู่แล้ว
    private const int SortingOrder = 500;

    private Sequence _sequence;
    private GameObject _root;
    private CanvasGroup _group;
    private RectTransform _barTop;
    private RectTransform _barBottom;
    private float _amount;

    private void Awake()
    {
        _sequence = GetComponent<Sequence>();
    }

    private void Update()
    {
        float target = _sequence != null && _sequence.IsPlaying ? 1f : 0f;
        _amount = Mathf.MoveTowards(_amount, target, Time.unscaledDeltaTime / _fadeTime);

        if (_amount <= 0f)
        {
            DestroyBars();
            return;
        }

        if (_root == null)
            CreateBars();

        float size = _size * Mathf.SmoothStep(0f, 1f, _amount);
        _barTop.anchorMin = new Vector2(0f, 1f - size);
        _barBottom.anchorMax = new Vector2(1f, size);

        // เมนูเปิด (Pause / Tutorial) ไม่ทับเมนู
        UIManager ui = UIManager.Instance;
        _group.alpha = ui != null && ui.HasAnyOpen ? 0f : 1f;
    }

    // ถูกปิด / เปลี่ยนซีนกลางคัน — ไม่ให้แถบดำค้างจอ
    private void OnDisable()
    {
        _amount = 0f;
        DestroyBars();
    }

    private void CreateBars()
    {
        _root = new GameObject($"{name}_Letterbox", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(_root, gameObject.scene);

        Canvas canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        _group = _root.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;

        _barTop = CreateBar("Top", new Vector2(0f, 1f), Vector2.one);
        _barBottom = CreateBar("Bottom", Vector2.zero, new Vector2(1f, 0f));
    }

    private RectTransform CreateBar(string barName, Vector2 anchorMin, Vector2 anchorMax)
    {
        var bar = new GameObject(barName, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(_root.transform, false);

        var rect = (RectTransform)bar.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = bar.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return rect;
    }

    private void DestroyBars()
    {
        if (_root != null)
            Destroy(_root);
        _root = null;
        _group = null;
        _barTop = null;
        _barBottom = null;
    }
}
