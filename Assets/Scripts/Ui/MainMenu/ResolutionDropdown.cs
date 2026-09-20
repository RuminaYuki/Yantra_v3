using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// เติม dropdown ความละเอียดจากรายการที่จอรองรับจริง
/// แปะบน GameObject ของ dropdown เอง
///
/// กรองเหลือเฉพาะที่คนใช้จริง ไม่งั้นจะมีตัวเลือกโผล่มา 20-30 อัน
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class ResolutionDropdown : MonoBehaviour
{
    [Header("ตัวกรอง")]
    [Tooltip("ตัดความละเอียดที่เล็กกว่านี้ทิ้ง — ต่ำกว่า 1280 UI จะเล็กจนอ่านไม่ออก")]
    [SerializeField] private int _minWidth = 1280;

    [Tooltip("ตัดตัวที่สัดส่วนไม่ตรงกับจอ กันภาพยืดหรือมีขอบดำ")]
    [SerializeField] private bool _matchAspectRatio = true;

    private TMP_Dropdown _dropdown;
    private readonly List<Resolution> _options = new();
    private bool _isRefreshing;

    private void Awake()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
        BuildOptions();
        _dropdown.onValueChanged.AddListener(OnSelected);
    }

    private void OnEnable()
    {
        SelectCurrent();
    }

    private void BuildOptions()
    {
        _options.Clear();
        _dropdown.ClearOptions();

        var labels = new List<string>();
        var seen = new HashSet<string>();

        var native = Screen.currentResolution;
        float targetAspect = (float)native.width / native.height;

        foreach (var res in Screen.resolutions)
        {
            if (res.width < _minWidth) continue;

            if (_matchAspectRatio)
            {
                float aspect = (float)res.width / res.height;
                if (Mathf.Abs(aspect - targetAspect) > 0.02f) continue;
            }

            // ตัดตัวซ้ำที่ต่างกันแค่ refresh rate ออก
            string key = res.width + " x " + res.height;
            if (!seen.Add(key)) continue;

            _options.Add(res);
            labels.Add(key);
        }

        // กรองแล้วไม่เหลืออะไร ใส่ของปัจจุบันกลับไปกัน dropdown ว่าง
        if (_options.Count == 0)
        {
            _options.Add(native);
            labels.Add(native.width + " x " + native.height);
        }

        // ตัวใหญ่สุดขึ้นก่อน
        _options.Reverse();
        labels.Reverse();

        _dropdown.AddOptions(labels);
    }

    private void SelectCurrent()
    {
        _isRefreshing = true;

        int index = _options.FindIndex(r =>
            r.width == GameSettings.ResolutionWidth &&
            r.height == GameSettings.ResolutionHeight);

        // ไม่เจอค่าที่เซฟไว้ (เช่นย้ายไปเล่นคนละจอ) ให้ใช้ความละเอียดปัจจุบัน
        if (index < 0)
            index = _options.FindIndex(r =>
                r.width == Screen.width && r.height == Screen.height);

        if (index < 0) index = 0;

        _dropdown.SetValueWithoutNotify(index);
        _dropdown.RefreshShownValue();

        _isRefreshing = false;
    }

    private void OnSelected(int index)
    {
        if (_isRefreshing) return;
        if (index < 0 || index >= _options.Count) return;

        var res = _options[index];
        int hz = Mathf.RoundToInt((float)res.refreshRateRatio.value);

        GameSettings.SetResolution(res.width, res.height, hz);
    }
}