using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Yantra.UI;

// จุดเดียวที่รวม feedback ตอนโดนตี — สั่นกล้อง + จอแดง
// ระบบ damage เรียกแค่ HurtFeedback.Instance.Play(...) พอ
public class HurtFeedback : MonoBehaviour
{
    public static HurtFeedback Instance { get; private set; }

    [Header("Camera Shake")]
    [Tooltip("trauma ตอนโดนตีเบาสุด")]
    [SerializeField] private float _minTrauma = 0.25f;

    [Tooltip("trauma ตอนโดนตีหนักสุด")]
    [SerializeField] private float _maxTrauma = 0.75f;

    [Header("Screen Flash (ไม่ใส่ก็ได้)")]
    [Tooltip("เว้นว่างไว้ — จะหาจาก UIRoot ตัวที่ใช้งานอยู่ให้เอง\n" +
             "ลากใส่เองไม่ได้อยู่แล้ว เพราะแผ่นแฟลชอยู่ใน UIRoot ที่ข้ามซีน")]
    [SerializeField] private Image _flashImage;

    [Tooltip("ชื่อ GameObject ของแผ่นแฟลชใน UIRoot")]
    [SerializeField] private string _flashObjectName = "HurtFlash";

    [SerializeField] private float _flashMaxAlpha = 0.35f;
    [SerializeField] private float _flashInDuration = 0.05f;
    [SerializeField] private float _flashOutDuration = 0.35f;

    private Coroutine _flashRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (!EnsureFlashImage())
            Debug.Log($"[HurtFeedback] ไม่พบแผ่นแฟลชชื่อ '{_flashObjectName}' — จะมีแค่จอสั่น", this);
    }

    // แผ่นแฟลชอยู่ข้ามซีน ถ้าซีนเปลี่ยนตอนกำลังแดงอยู่ จอจะแดงค้าง
    private void OnDisable()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        SetFlashAlpha(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Play(float normalizedDamage = 0.5f)
    {
        normalizedDamage = Mathf.Clamp01(normalizedDamage);

        if (CameraShaker.Instance != null)
            CameraShaker.Instance.AddTrauma(Mathf.Lerp(_minTrauma, _maxTrauma, normalizedDamage));

        if (!EnsureFlashImage())
            return;

        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine(normalizedDamage));
    }

    public void PlayFromDamage(float damage, float maxHealth)
    {
        if (maxHealth <= 0f) { Play(0.5f); return; }
        Play(damage / maxHealth);
    }

    // แผ่นที่หาไว้อาจโดนลบไปแล้ว — เช็คทุกครั้งก่อนใช้ หายก็หาใหม่
    private bool EnsureFlashImage()
    {
        if (_flashImage != null)
            return true;

        _flashImage = FindFlashImage();
        if (_flashImage == null)
            return false;

        _flashImage.raycastTarget = false;
        SetFlashAlpha(0f);
        return true;
    }

    // หาจาก UIRoot ตัวที่อยู่รอด (ตัวที่มี UIManager.Instance) ก่อน
    // ตอนเข้าซีนใหม่ UIRoot ตัวซ้ำยังไม่ถูกลบจนจบเฟรม ถ้าหาทั้งซีนอาจหยิบได้แผ่นที่กำลังจะหาย
    private Image FindFlashImage()
    {
        if (string.IsNullOrEmpty(_flashObjectName))
            return null;

        UIManager ui = UIManager.Instance;
        if (ui != null)
        {
            foreach (Image image in ui.transform.root.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject.name == _flashObjectName)
                    return image;
            }
        }

        foreach (Image image in FindObjectsByType<Image>(FindObjectsInactive.Include))
        {
            if (image.gameObject.name == _flashObjectName)
                return image;
        }

        return null;
    }

    private IEnumerator FlashRoutine(float strength)
    {
        float peak = _flashMaxAlpha * Mathf.Lerp(0.6f, 1f, strength);

        yield return FadeAlpha(_flashImage.color.a, peak, _flashInDuration);
        yield return FadeAlpha(peak, 0f, _flashOutDuration);

        _flashRoutine = null;
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetFlashAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetFlashAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetFlashAlpha(to);
    }

    private void SetFlashAlpha(float alpha)
    {
        if (_flashImage == null) return;
        var c = _flashImage.color;
        c.a = alpha;
        _flashImage.color = c;
    }
}