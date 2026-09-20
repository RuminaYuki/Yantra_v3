using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HurtFeedback : MonoBehaviour
{
    public static HurtFeedback Instance { get; private set; }

    [Header("Camera Shake")]
    [Tooltip("trauma ตอนโดนตีเบาสุด")]
    [SerializeField] private float _minTrauma = 0.25f;

    [Tooltip("trauma ตอนโดนตีหนักสุด")]
    [SerializeField] private float _maxTrauma = 0.75f;

    [Header("Screen Flash (ไม่ใส่ก็ได้)")]
    [Tooltip("Image สีแดงเต็มจอ ตั้ง alpha = 0 ไว้ และ Raycast Target ติ๊กออก\n" +
             "เว้นว่างได้ จะหาจากชื่อ GameObject ให้เอง")]
    [SerializeField] private Image _flashImage;

    [Tooltip("ชื่อ GameObject ของแผ่นแฟลช ใช้ตอนหาอัตโนมัติ\n" +
             "จำเป็นเพราะแผ่นแฟลชอยู่ใน prefab UIRoot ส่วนตัวนี้อยู่ใน scene\n" +
             "Unity ลาก reference ข้ามกันไม่ได้ เลยต้องหาตอน runtime แทน")]
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
        // หาใน Start ไม่ใช่ Awake เพื่อให้ UIRoot สร้างเสร็จก่อน
        if (_flashImage == null) _flashImage = FindFlashImage();

        if (_flashImage != null)
        {
            _flashImage.raycastTarget = false;
            SetFlashAlpha(0f);
        }
        else
        {
            Debug.Log($"[HurtFeedback] ไม่พบแผ่นแฟลชชื่อ '{_flashObjectName}' — จะมีแค่จอสั่น", this);
        }
    }

    /// <summary>
    /// หาจากชื่อ แม้ GameObject จะปิดอยู่ก็ตาม
    /// FindObjectsByType หาเฉพาะที่ active เลยต้องไล่จาก UIRoot เอง
    /// </summary>
    private Image FindFlashImage()
    {
        if (string.IsNullOrEmpty(_flashObjectName)) return null;

        var allImages = FindObjectsByType<Image>(
            FindObjectsInactive.Include);

        foreach (var image in allImages)
        {
            if (image.gameObject.name == _flashObjectName) return image;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// เรียกตอนผู้เล่นโดนตี
    /// normalizedDamage: 0 = เบาสุด, 1 = หนักสุด
    /// </summary>
    public void Play(float normalizedDamage = 0.5f)
    {
        normalizedDamage = Mathf.Clamp01(normalizedDamage);

        if (CameraShaker.Instance != null)
            CameraShaker.Instance.AddTrauma(Mathf.Lerp(_minTrauma, _maxTrauma, normalizedDamage));

        if (_flashImage != null)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(normalizedDamage));
        }
    }

    /// <summary>เวอร์ชันสะดวก — ส่งค่าดาเมจกับเลือดเต็มมา แล้วมันคำนวณเอง</summary>
    public void PlayFromDamage(float damage, float maxHealth)
    {
        if (maxHealth <= 0f) { Play(0.5f); return; }
        Play(damage / maxHealth);
    }

    private IEnumerator FlashRoutine(float strength)
    {
        float peak = _flashMaxAlpha * Mathf.Lerp(0.6f, 1f, strength);

        // ขึ้นเร็ว
        yield return FadeAlpha(_flashImage.color.a, peak, _flashInDuration);
        // ลงช้า
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