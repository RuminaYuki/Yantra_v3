using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// จัดการ tab ซ้าย-ขวาในหน้า Settings
/// ไม่ใช่ UIScreen และไม่เข้า stack — เป็นแค่การสลับ panel ข้างในหน้าเดียว
/// วางบน SettingsScreen (ตัวเดียวกับ SettingsScreen.cs)
/// </summary>
public class SettingsTabController : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        [Tooltip("ปุ่มหมวดทางซ้าย")]
        public Button button;

        [Tooltip("panel ทางขวาที่จะโชว์ตอนเลือกหมวดนี้")]
        public GameObject panel;

        [Tooltip("คำอธิบายที่โชว์ด้านล่าง")]
        [TextArea(1, 2)] public string description;

        [Tooltip("ตัวที่จะถูกเลือกตอนกด → เข้า panel ขวา (สำหรับ gamepad)")]
        public GameObject firstSelectedInPanel;
    }

    [Header("Tabs")]
    [SerializeField] private List<Tab> _tabs = new();

    [Header("Description")]
    [SerializeField] private TMP_Text _descriptionText;

    [Header("Highlight")]
    [Tooltip("แถบเล็ก ๆ ที่เลื่อนไปหน้าหมวดที่เลือกอยู่ ไม่ใส่ก็ได้")]
    [SerializeField] private RectTransform _selectionMarker;

    [Header("Colors")]
    [SerializeField] private Color _activeColor   = Color.white;
    [SerializeField] private Color _inactiveColor = new(0.6f, 0.6f, 0.6f, 1f);

    private int _currentIndex = -1;

    /// <summary>หมวดที่เปิดอยู่ตอนนี้</summary>
    public int CurrentIndex => _currentIndex;

    private void Awake()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            int index = i;   // capture ค่า ไม่งั้นทุกปุ่มจะชี้ไปตัวสุดท้าย
            if (_tabs[i].button != null)
                _tabs[i].button.onClick.AddListener(() => SelectTab(index));
        }
    }

    private void OnEnable()
    {
        // เปิดหน้ามาให้อยู่หมวดแรกเสมอ
        SelectTab(0, force: true);
    }

    public void SelectTab(int index, bool force = false)
    {
        if (index < 0 || index >= _tabs.Count) return;
        if (!force && index == _currentIndex) return;

        _currentIndex = index;

        for (int i = 0; i < _tabs.Count; i++)
        {
            bool active = (i == index);

            if (_tabs[i].panel != null)
                _tabs[i].panel.SetActive(active);

            SetButtonTint(_tabs[i].button, active);
        }

        if (_descriptionText != null)
            _descriptionText.text = _tabs[index].description;

        MoveMarker(_tabs[index].button);
    }

    /// <summary>
    /// เรียกตอนคนเล่นกดยืนยันที่ปุ่มหมวด — ย้าย selection ไปฝั่งขวา
    /// ใช้ตอนเล่นด้วย gamepad
    /// </summary>
    public void FocusPanel()
    {
        if (_currentIndex < 0) return;

        var target = _tabs[_currentIndex].firstSelectedInPanel;
        if (target != null && target.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);
    }

    // ---------- ภายใน ----------

    private void SetButtonTint(Button button, bool active)
    {
        if (button == null) return;

        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.color = active ? _activeColor : _inactiveColor;
    }

    private void MoveMarker(Button button)
    {
        if (_selectionMarker == null || button == null) return;

        var target = button.GetComponent<RectTransform>();
        var pos = _selectionMarker.anchoredPosition;
        pos.y = target.anchoredPosition.y;
        _selectionMarker.anchoredPosition = pos;
    }
}
