using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[Serializable]
public struct TutorialPage
{
    [Tooltip("รูปประกอบ — เว้นว่างได้ ช่องรูปจะซ่อนเอง")]
    public Sprite Image;

    [Tooltip("คลิปสาธิต — ใส่แล้วเล่นแทนรูป วนซ้ำ ไม่มีเสียง (แนะนำ 3-6 วิ, 1280x720, MP4)\nเว้นว่าง = ใช้รูปตามปกติ")]
    public VideoClip Video;

    public string Title;

    [TextArea(3, 8)]
    public string Body;
}

public enum TutorialKey
{
    E,
    Tab,
    F
}

// tutorial 1 ชุด — กด + ที่ Pages เพื่อเพิ่มหน้า
[CreateAssetMenu(fileName = "NewTutorial", menuName = "Yantra/Tutorial/Tutorial Data")]
public class TutorialDataSO : ScriptableObject
{
    [Tooltip("Name stored in the save file. Filled from the asset name.\n" +
         "Keep it the same after release, even if the asset is renamed.")]
    [SerializeField] private string _saveKey;

    [Tooltip("ปุ่มถัดไป/ปิด จะกดไม่ได้กี่วินาทีในแต่ละหน้า — บังคับให้ผู้เล่นได้อ่านก่อน")]
    [SerializeField, Min(0f)] private float _lockSeconds = 2f;

    [SerializeField] private TutorialPage[] _pages;

    [Header("บังคับกดปุ่ม (หน้าสุดท้าย)")]
    [Tooltip("ปุ่มที่ต้องกดเพื่อปิดหน้าสุดท้าย — บังคับให้ผู้เล่นลองกดปุ่มนั้นจริง\nหน้าก่อนหน้ายังกด E ไปต่อตามปกติ")]
    [SerializeField] private TutorialKey _closeKey = TutorialKey.E;

    [Tooltip("ข้อความปุ่มหน้าสุดท้าย เช่น {Tab} Open Inventory\nเว้นว่าง = ใช้ข้อความปกติ แต่เปลี่ยนไอคอนเป็นปุ่มที่บังคับ")]
    [SerializeField] private string _closeLabel;

    [Tooltip("ยิงตอนผู้เล่นกดปุ่มปิดหน้าสุดท้าย — ไว้ต่อกับระบบอื่น เช่น สั่งเปิดกระเป๋า (เว้นว่างได้)")]
    [SerializeField] private VoidEventChannelSO _onClosed;

    public string SaveKey => string.IsNullOrEmpty(_saveKey) ? name : _saveKey;

    public float LockSeconds => _lockSeconds;
    public IReadOnlyList<TutorialPage> Pages => _pages ?? Array.Empty<TutorialPage>();
    public TutorialKey CloseKey => _closeKey;
    public string CloseLabel => _closeLabel;
    public VoidEventChannelSO OnClosed => _onClosed;

#if UNITY_EDITOR
    // เตือนตั้งแต่ตอนพิมพ์ ถ้าใส่ {ชื่อ} ที่ไม่มีไอคอน — ไม่ต้องรอกด Play ไปเจอ
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_saveKey)) _saveKey = name;
        UnityEditor.EditorApplication.delayCall -= CheckIcons;
        UnityEditor.EditorApplication.delayCall += CheckIcons;
    }

    private void CheckIcons()
    {
        if (this == null || _pages == null)
            return;

        TMPro.TMP_SpriteAsset asset = TMPro.TMP_Settings.defaultSpriteAsset;

        for (int i = 0; i < _pages.Length; i++)
        {
            string text = _pages[i].Title + " " + _pages[i].Body;
            if (i == _pages.Length - 1) text += " " + _closeLabel;

            var missing = InputIconText.FindMissing(text, asset);
            if (missing.Count == 0)
                continue;

            string names = "{" + string.Join("}, {", missing) + "}";
            Debug.LogWarning($"[{name}] หน้า {i + 1}: ไม่มีไอคอน {names} — เช็คตัวสะกด (ตัวเล็กตัวใหญ่ต้องตรง)", this);
        }
    }
#endif
}