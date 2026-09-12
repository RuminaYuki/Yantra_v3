using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yantra.UI;

public class LoadingScreen : UIScreen
{
    [Header("Progress (ไม่ใส่ก็ได้)")]
    [SerializeField] private Slider _progressBar;
    [SerializeField] private TMP_Text _percentText;

    [Tooltip("ความเร็วที่แถบวิ่งตามค่าจริง — ยิ่งต่ำยิ่งลื่นแต่หน่วง")]
    [SerializeField] private float _smoothSpeed = 4f;

    [Header("Press Any Key")]
    [SerializeField] private GameObject _pressAnyKeyPrompt;

    [Tooltip("ซ่อน spinner ตอนขึ้นข้อความให้กดปุ่ม")]
    [SerializeField] private GameObject _spinner;

    [Header("Tips")]
    [SerializeField] private TMP_Text _tipText;
    [SerializeField] private string[] _tips;

    private float _target;
    private float _displayed;

    protected override void OnOpening()
    {
        _target = 0f;
        _displayed = 0f;
        ApplyToUI(0f);

        if (_pressAnyKeyPrompt != null) _pressAnyKeyPrompt.SetActive(false);
        if (_spinner != null) _spinner.SetActive(true);

        if (_tipText != null && _tips != null && _tips.Length > 0)
            _tipText.text = _tips[Random.Range(0, _tips.Length)];
    }

    /// <summary>เรียกโดย SceneLoader — ค่า 0..1</summary>
    public void SetProgress(float value)
    {
        _target = Mathf.Clamp01(value);
    }

    public void ShowPressAnyKey(bool show)
    {
        if (_pressAnyKeyPrompt != null) _pressAnyKeyPrompt.SetActive(show);

        // โหลดเสร็จแล้ว ไม่ต้องหมุนต่อ ไม่งั้นดูเหมือนยังทำงานอยู่
        if (_spinner != null) _spinner.SetActive(!show);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (_progressBar == null && _percentText == null) return;

        // ไล่ตามค่าจริงแบบนุ่ม ๆ ไม่ให้แถบกระโดดจาก 0 ไป 100 ทีเดียว
        _displayed = Mathf.MoveTowards(_displayed, _target, _smoothSpeed * Time.unscaledDeltaTime);
        ApplyToUI(_displayed);
    }

    private void ApplyToUI(float value)
    {
        if (_progressBar != null) _progressBar.value = value;
        if (_percentText != null) _percentText.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}