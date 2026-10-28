using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yantra.UI;

// วางใน UIRoot ข้างบน Fader (Fader ต้องเป็นลูกตัวสุดท้ายเหมือนเดิม) — ห้ามวางใต้ HUD เพราะ HUD ซ่อนตัวตอน Glory
[RequireComponent(typeof(RectTransform))]
public class ScreenBloodOverlay : MonoBehaviour
{
    public static ScreenBloodOverlay Instance { get; private set; }

    [Header("รูปคราบเลือด")]
    [Tooltip("รูป PNG พื้นใส ใส่ได้หลายรูป ระบบจะสุ่มเอง")]
    [SerializeField] private Texture2D[] _splats = new Texture2D[0];

    [Tooltip("สีคูณทับรูป — ขาว = ใช้สีเดิมของรูป")]
    [SerializeField] private Color _tint = Color.white;

    [Header("จำนวน / ขนาด / ตำแหน่ง")]
    [SerializeField, Min(1)] private int _minCount = 2;
    [SerializeField, Min(1)] private int _maxCount = 4;

    [Tooltip("ขนาดเล็กสุด เทียบกับความสูงจอ (0.3 = 30% ของจอ)")]
    [SerializeField, Range(0.05f, 1.5f)] private float _minSize = 0.35f;

    [Tooltip("ขนาดใหญ่สุด เทียบกับความสูงจอ")]
    [SerializeField, Range(0.05f, 1.5f)] private float _maxSize = 0.7f;

    [Tooltip("กันเลือดบังกลางจอ\n0 = แปะกลางจอได้ / 0.5 = ต้องห่างจากกลางจอครึ่งทางถึงขอบ")]
    [SerializeField, Range(0f, 0.9f)] private float _keepCenterClear = 0.35f;

    [Header("จังหวะ (วินาที)")]
    [Tooltip("เวลาที่เลือดโผล่ขึ้นมา — สั้นๆ จะรู้สึกเหมือนสาดใส่")]
    [SerializeField, Min(0f)] private float _popTime = 0.06f;

    [Tooltip("ค้างเต็มๆ อยู่กี่วินาทีก่อนเริ่มจาง")]
    [SerializeField, Min(0f)] private float _holdTime = 1.2f;

    [SerializeField, Min(0.01f)] private float _fadeTime = 1.5f;

    [Tooltip("ทั้งคราบเลื่อนลงกี่ส่วนของความสูงจอ (0.015 = 1.5%)\nเลือดจริงติดกระจกแน่น เลื่อนนิดเดียวพอ เยอะไปจะดูเหมือนสติกเกอร์ไหล")]
    [SerializeField, Range(0f, 0.3f)] private float _slideDistance = 0.015f;

    [Tooltip("ยืดลงกี่ส่วนของขนาดคราบ เหมือนเลือดค่อยๆ ไหลย้อยลง (0.1 = ยาวขึ้น 10%)")]
    [SerializeField, Range(0f, 0.5f)] private float _dripStretch = 0.1f;

    [Header("Options")]
    [Tooltip("ซ่อนเลือดตอนเปิดเมนู (Pause / Settings) ไม่ให้ทับเมนู")]
    [SerializeField] private bool _hideWhenMenuOpen = true;

    [Tooltip("คราบบนจอพร้อมกันได้สูงสุดกี่อัน เกินนี้จะเอาอันเก่าสุดมาใช้ใหม่")]
    [SerializeField, Min(1)] private int _maxOnScreen = 12;

    [Header("Debug")]
    [SerializeField] private bool _logSplash;

    private sealed class Splat
    {
        public RawImage Image;
        public RectTransform Rect;
        public Vector2 StartPosition;
        public float Mirror;
        public float Age;
        public bool Active;
    }

    private readonly List<Splat> _pool = new List<Splat>();
    private RectTransform _rect;
    private CanvasGroup _group;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        // UIRoot ตัวซ้ำตอนกลับเมนูหลักจะถูกลบทิ้งท้ายเฟรม — ไม่แย่งตำแหน่งตัวจริง
        if (Instance != null && Instance != this)
            return;
        Instance = this;

        // ขยายเต็มจอให้เอง ไม่ต้องไปตั้ง Anchor
        _rect = GetComponent<RectTransform>();
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;

        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
    }

    private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleSceneChanged;
        ClearAll();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // เปลี่ยนฉากตอนเลือดยังอยู่บนจอ — ล้างทิ้ง ไม่งั้นค้างทับหน้าโหลด
    private void HandleSceneChanged(Scene from, Scene to) => ClearAll();

    public void Splash()
    {
        if (Instance != this || !isActiveAndEnabled)
            return;

        if (_splats == null || _splats.Length == 0)
        {
            Debug.LogWarning("[ScreenBlood] ยังไม่ได้ใส่รูปคราบเลือดในช่อง Splats", this);
            return;
        }

        int count = Random.Range(Mathf.Min(_minCount, _maxCount), Mathf.Max(_minCount, _maxCount) + 1);
        for (int i = 0; i < count; i++)
            Spawn();

        if (_logSplash)
            Debug.Log($"[ScreenBlood] สาดเลือด {count} คราบ", this);
    }

    [ContextMenu("ทดสอบ: สาดเลือด")]
    private void TestSplash() => Splash();

    private void Spawn()
    {
        Texture2D texture = _splats[Random.Range(0, _splats.Length)];
        if (texture == null)
            return;

        Splat splat = GetFreeSplat();
        Rect area = _rect.rect;

        float height = area.height * Random.Range(Mathf.Min(_minSize, _maxSize), Mathf.Max(_minSize, _maxSize));
        float width = height * texture.width / Mathf.Max(1f, texture.height);

        // จุดหมุนอยู่ขอบบนของคราบ ตอนยืดจะได้ย้อยลงล่างอย่างเดียว
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(_keepCenterClear, 1f);
        splat.StartPosition = new Vector2(
            Mathf.Cos(angle) * distance * area.width * 0.5f,
            Mathf.Sin(angle) * distance * area.height * 0.5f + height * 0.5f);

        splat.Image.texture = texture;
        splat.Rect.sizeDelta = new Vector2(width, height);
        splat.Rect.anchoredPosition = splat.StartPosition;
        splat.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
        splat.Rect.SetAsLastSibling();
        splat.Mirror = Random.value < 0.5f ? -1f : 1f;
        splat.Age = 0f;
        splat.Active = true;

        Apply(splat, 0f, 0.8f, 0f, 0f);
        splat.Image.gameObject.SetActive(true);
    }

    private Splat GetFreeSplat()
    {
        Splat oldest = null;
        foreach (Splat splat in _pool)
        {
            if (!splat.Active)
                return splat;
            if (oldest == null || splat.Age > oldest.Age)
                oldest = splat;
        }

        if (_pool.Count < _maxOnScreen || oldest == null)
            return CreateSplat();

        return oldest;
    }

    private Splat CreateSplat()
    {
        var go = new GameObject("BloodSplat", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        go.SetActive(false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);

        var image = go.GetComponent<RawImage>();
        image.raycastTarget = false;

        var splat = new Splat { Image = image, Rect = rect };
        _pool.Add(splat);
        return splat;
    }

    private void Update()
    {
        if (Instance != this)
            return;

        bool menuOpen = _hideWhenMenuOpen && UIManager.Instance != null && UIManager.Instance.HasAnyOpen;
        _group.alpha = menuOpen ? 0f : 1f;

        // ใช้เวลาเกม — กด Pause แล้วเลือดหยุดค้างไว้ ไม่จางหายไปหลังเมนู
        float dt = Time.deltaTime;
        float life = _popTime + _holdTime + _fadeTime;
        float screenHeight = _rect.rect.height;

        foreach (Splat splat in _pool)
        {
            if (!splat.Active)
                continue;

            splat.Age += dt;
            if (splat.Age >= life)
            {
                Deactivate(splat);
                continue;
            }

            float alpha = 1f;
            float scale = 1f;

            if (splat.Age < _popTime)
            {
                float k = splat.Age / Mathf.Max(0.0001f, _popTime);
                alpha = k;
                scale = Mathf.Lerp(0.8f, 1f, 1f - (1f - k) * (1f - k));
            }
            else if (splat.Age > _popTime + _holdTime)
            {
                float k = (splat.Age - _popTime - _holdTime) / _fadeTime;
                alpha = Mathf.SmoothStep(1f, 0f, k);
            }

            // ไหลช้าตอนแรก แล้วค่อยเร็วขึ้นเหมือนเลือดหนืดๆ ไหลลงกระจก
            float flow = Mathf.Pow(splat.Age / life, 1.4f);
            Apply(splat, alpha, scale, flow * _slideDistance * screenHeight, flow * _dripStretch);
        }
    }

    private void Apply(Splat splat, float alpha, float scale, float slide, float stretch)
    {
        splat.Rect.anchoredPosition = splat.StartPosition + Vector2.down * slide;
        splat.Rect.localScale = new Vector3(splat.Mirror * scale, scale * (1f + stretch), 1f);

        Color color = _tint;
        color.a *= alpha;
        splat.Image.color = color;
    }

    private void Deactivate(Splat splat)
    {
        splat.Active = false;
        if (splat.Image != null)
            splat.Image.gameObject.SetActive(false);
    }

    public void ClearAll()
    {
        foreach (Splat splat in _pool)
            Deactivate(splat);
    }
}